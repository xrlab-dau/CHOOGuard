"""Add bounded accessibility structure to this proposal's ReportLab PDF.

The exporter must already wrap semantic page content in marked-content
sequences, for example ``/H1 <</MCID 0>> BDC ... EMC``. ``page_tags`` supplies
one list per page, in intended reading order, containing dictionaries such as
``{"mcid": 0, "role": "H1"}`` or
``{"mcid": 2, "role": "Figure", "alt": "A description of the image."}``.
MCIDs are page-local. Figures require an informative description from the
exporter; this module cannot assess the description's meaning.

Only structure/catalog/page metadata is appended. The temporary result is
checked before it replaces the input. Existing tagged documents, inline-image
content streams, and encrypted/repaired PDFs are deliberately out of scope.
This is tagging support, not PDF/UA or assistive-technology certification.

References:
https://pymupdf.readthedocs.io/en/latest/document.html#Document.xref_set_key
https://pdfa.org/wp-content/uploads/2023/07/Tagged-PDF-Best-Practice-Guide.pdf
"""

from __future__ import annotations

from collections import Counter
from collections.abc import Mapping, Sequence
from hashlib import sha256
import os
from pathlib import Path
import re
import shutil
import tempfile

import pymupdf


_ROLES = frozenset({"H1", "H2", "H3", "H4", "H5", "H6", "P", "Figure"})
_INTEGER = re.compile(rb"[+-]?\d+\Z")
_SPACE = b"\x00\t\n\x0c\r "
_DELIMITERS = _SPACE + b"()<>[]{}/%"


class PDFTaggingError(ValueError):
    """The source or semantic metadata does not meet the export contract."""


def _tokens(data: bytes):
    """Read relevant PDF tokens, ignoring comments, strings and hex strings.

    ReportLab uses external image XObjects. Inline images need a binary-aware
    parser, so they are rejected rather than allowing binary data to masquerade
    as PDF operators. Literal strings can themselves contain BDC/EMC text.
    """
    pos, length = 0, len(data)
    while pos < length:
        char = data[pos]
        if char in _SPACE:
            pos += 1
        elif char == ord("%"):
            while pos < length and data[pos] not in b"\r\n":
                pos += 1
        elif char == ord("("):
            pos, depth = pos + 1, 1
            while pos < length and depth:
                char = data[pos]
                if char == ord("\\"):
                    pos += 2
                    continue
                depth += (char == ord("(")) - (char == ord(")"))
                pos += 1
            if depth:
                raise PDFTaggingError("Unterminated PDF literal string")
            yield b"STRING"
        elif data[pos:pos + 2] in (b"<<", b">>"):
            yield data[pos:pos + 2]
            pos += 2
        elif char == ord("<"):
            end = data.find(b">", pos + 1)
            if end < 0:
                raise PDFTaggingError("Unterminated PDF hex string")
            pos = end + 1
            yield b"STRING"
        elif char in b"[]{}>":
            yield data[pos:pos + 1]
            pos += 1
        else:
            start = pos
            pos += 1
            while pos < length and data[pos] not in _DELIMITERS:
                pos += 1
            token = data[start:pos]
            if token == b"BI":
                raise PDFTaggingError("Inline images are outside this ReportLab export contract")
            yield token


def _marked_content(page) -> dict[int, str]:
    tokens = list(_tokens(page.read_contents()))
    found, stack = {}, []
    for index, token in enumerate(tokens):
        if token == b"BMC":
            stack.append(None)
        elif token == b"BDC":
            mcid = None
            if index and tokens[index - 1] == b">>":
                depth, start = 1, index - 2
                while start >= 0:
                    if tokens[start] == b">>":
                        depth += 1
                    elif tokens[start] == b"<<":
                        depth -= 1
                        if not depth:
                            break
                    start -= 1
                if start <= 0:
                    raise PDFTaggingError("Malformed marked-content property dictionary")
                properties = tokens[start + 1:index - 1]
                positions = [i for i, item in enumerate(properties) if item == b"/MCID"]
                if positions:
                    position = positions[0] + 1
                    if (len(positions) != 1 or position >= len(properties)
                            or not _INTEGER.fullmatch(properties[position])):
                        raise PDFTaggingError("A BDC must contain one integer MCID")
                    mcid = int(properties[position])
                    role_token = tokens[start - 1]
                    if mcid < 0 or not role_token.startswith(b"/"):
                        raise PDFTaggingError("Invalid MCID or semantic role in BDC")
                    role = role_token[1:].decode("ascii")
                    if mcid in found:
                        raise PDFTaggingError(f"Page {page.number + 1}: duplicate MCID {mcid}")
                    if any(item is not None for item in stack):
                        raise PDFTaggingError("Nested semantic MCIDs are outside this export contract")
                    found[mcid] = role
            stack.append(mcid)
        elif token == b"EMC":
            if not stack:
                raise PDFTaggingError("EMC has no matching BDC/BMC")
            stack.pop()
    if stack:
        raise PDFTaggingError("A BDC/BMC has no matching EMC")
    return found


def _normalize_tags(page_tags, count: int):
    if not isinstance(page_tags, Sequence) or isinstance(page_tags, (str, bytes)):
        raise PDFTaggingError("page_tags must be a sequence of per-page tag sequences")
    if len(page_tags) != count:
        raise PDFTaggingError(f"Expected {count} page tag lists, got {len(page_tags)}")
    normalized = []
    for page_number, tags in enumerate(page_tags, 1):
        if not isinstance(tags, Sequence) or isinstance(tags, (str, bytes)):
            raise PDFTaggingError(f"Page {page_number}: tags must be a sequence")
        page, seen = [], set()
        for tag in tags:
            if not isinstance(tag, Mapping):
                raise PDFTaggingError(f"Page {page_number}: each tag must be a mapping")
            mcid, role, alt = tag.get("mcid"), tag.get("role"), tag.get("alt")
            if type(mcid) is not int or not 0 <= mcid < 100_000:
                raise PDFTaggingError(f"Page {page_number}: MCID must be an integer in 0..99999")
            if mcid in seen:
                raise PDFTaggingError(f"Page {page_number}: duplicate metadata MCID {mcid}")
            if not isinstance(role, str) or role.removeprefix("/") not in _ROLES:
                raise PDFTaggingError(f"Page {page_number}: unsupported semantic role {role!r}")
            role = role.removeprefix("/")
            if alt is not None and (not isinstance(alt, str) or not alt.strip()):
                raise PDFTaggingError(f"Page {page_number}: Alt must be a nonempty string")
            if role == "Figure" and alt is None:
                raise PDFTaggingError(f"Page {page_number}: Figure MCID {mcid} requires Alt")
            seen.add(mcid)
            page.append({"mcid": mcid, "role": role, "alt": alt})
        normalized.append(page)
    return normalized


def _plain(value):
    if isinstance(value, Mapping):
        return {key: _plain(item) for key, item in value.items()}
    if isinstance(value, (list, tuple, pymupdf.Rect, pymupdf.Point)):
        return tuple(_plain(item) for item in value)
    return value


def _snapshot(doc):
    """Fingerprint the original semantic content, objects and 72 dpi renders."""
    mutable = {doc.pdf_catalog(), *(page.xref for page in doc)}
    objects, streams = {}, {}
    for xref in range(1, doc.xref_length()):
        if xref not in mutable:
            objects[xref] = doc.xref_object(xref, compressed=False)
        if doc.xref_is_stream(xref):
            streams[xref] = sha256(doc.xref_stream_raw(xref)).hexdigest()
    pages = []
    for page in doc:
        pixmap = page.get_pixmap(matrix=pymupdf.Matrix(1, 1), colorspace=pymupdf.csRGB,
                                 alpha=False, annots=True)
        pages.append({
            "geometry": (tuple(page.mediabox), tuple(page.cropbox), page.rotation),
            "text": page.get_text("text", sort=False),
            "links": _plain(page.get_links()),
            "fonts": _plain(page.get_fonts(full=True)),
            "images": _plain(page.get_images(full=True)),
            "render": (pixmap.width, pixmap.height, pixmap.n, sha256(pixmap.samples).hexdigest()),
        })
    return {"pages": pages, "objects": objects, "streams": streams}


def _reference(xref: int) -> str:
    return f"{xref} 0 R"


def _references(xrefs) -> str:
    return "[" + " ".join(_reference(xref) for xref in xrefs) + "]"


def _unicode_string(value: str) -> str:
    return "<FEFF" + value.encode("utf-16-be").hex().upper() + ">"


def _add_structure(doc, page_tags):
    root, document, parent_tree = [doc.get_new_xref() for _ in range(3)]
    sections, elements, parent_entries = [], [], []
    for page_number, tags in enumerate(page_tags):
        page = doc[page_number]
        section = doc.get_new_xref()
        sections.append(section)
        children = []
        parents = ["null"] * (max((tag["mcid"] for tag in tags), default=-1) + 1)
        for tag in tags:
            element = doc.get_new_xref()
            children.append(element)
            parents[tag["mcid"]] = _reference(element)
            alternative = "" if tag["alt"] is None else " /Alt " + _unicode_string(tag["alt"])
            doc.update_object(element,
                f"<< /Type /StructElem /S /{tag['role']} /P {_reference(section)} "
                f"/Pg {_reference(page.xref)} /K {tag['mcid']}{alternative} >>")
            elements.append((page_number, section, element, tag))
        doc.update_object(section,
            f"<< /Type /StructElem /S /Sect /P {_reference(document)} "
            f"/Pg {_reference(page.xref)} /K {_references(children)} >>")
        doc.xref_set_key(page.xref, "StructParents", str(page_number))
        parent_entries.append(f"{page_number} [{' '.join(parents)}]")
    doc.update_object(document,
        f"<< /Type /StructElem /S /Document /P {_reference(root)} "
        f"/K {_references(sections)} /Lang (ko-KR) >>")
    doc.update_object(parent_tree, "<< /Nums [" + " ".join(parent_entries) + "] >>")
    doc.update_object(root,
        f"<< /Type /StructTreeRoot /K {_reference(document)} "
        f"/ParentTree {_reference(parent_tree)} /ParentTreeNextKey {len(page_tags)} >>")
    catalog = doc.pdf_catalog()
    doc.xref_set_key(catalog, "StructTreeRoot", _reference(root))
    doc.xref_set_key(catalog, "Lang", "(ko-KR)")
    doc.xref_set_key(catalog, "MarkInfo", "<< /Marked true >>")
    return {"root": root, "document": document, "parent_tree": parent_tree,
            "sections": sections, "elements": elements,
            "parent_nums": "[" + " ".join(parent_entries) + "]"}


def _verify_structure(doc, tree, page_tags):
    def expect(xref, key, expected):
        actual = doc.xref_get_key(xref, key)[1]
        if actual != expected:
            raise PDFTaggingError(f"Structure verification failed for {xref}/{key}: {actual!r}")

    catalog = doc.pdf_catalog()
    expect(catalog, "StructTreeRoot", _reference(tree["root"]))
    expect(catalog, "Lang", "ko-KR")
    expect(catalog, "MarkInfo/Marked", "true")
    expect(tree["root"], "Type", "/StructTreeRoot")
    expect(tree["root"], "K", _reference(tree["document"]))
    expect(tree["root"], "ParentTree", _reference(tree["parent_tree"]))
    expect(tree["root"], "ParentTreeNextKey", str(len(page_tags)))
    expect(tree["document"], "Type", "/StructElem")
    expect(tree["document"], "S", "/Document")
    expect(tree["document"], "P", _reference(tree["root"]))
    expect(tree["document"], "Lang", "ko-KR")
    expected_sections = list(_tokens(_references(tree["sections"]).encode()))
    if list(_tokens(doc.xref_get_key(tree["document"], "K")[1].encode())) != expected_sections:
        raise PDFTaggingError("Document section order differs from page order")
    parent_nums = doc.xref_get_key(tree["parent_tree"], "Nums")[1]
    if list(_tokens(parent_nums.encode())) != list(_tokens(tree["parent_nums"].encode())):
        raise PDFTaggingError("ParentTree MCID mapping differs from semantic metadata")
    for page_number, (page, section, tags) in enumerate(zip(doc, tree["sections"], page_tags)):
        expect(page.xref, "StructParents", str(page_number))
        expect(section, "Type", "/StructElem")
        expect(section, "S", "/Sect")
        expect(section, "P", _reference(tree["document"]))
        expect(section, "Pg", _reference(page.xref))
        children = [element for number, _, element, _ in tree["elements"] if number == page_number]
        actual = list(_tokens(doc.xref_get_key(section, "K")[1].encode()))
        if actual != list(_tokens(_references(children).encode())):
            raise PDFTaggingError(f"Page {page_number + 1}: semantic reading order changed")
        if _marked_content(page) != {tag["mcid"]: tag["role"] for tag in tags}:
            raise PDFTaggingError(f"Page {page_number + 1}: MCIDs differ after tagging")
    for page_number, section, element, tag in tree["elements"]:
        expect(element, "Type", "/StructElem")
        expect(element, "S", "/" + tag["role"])
        expect(element, "P", _reference(section))
        expect(element, "Pg", _reference(doc[page_number].xref))
        expect(element, "K", str(tag["mcid"]))
        if tag["alt"] is not None:
            expect(element, "Alt", tag["alt"])


def tag_pdf(path, page_tags) -> dict:
    """Tag an untagged ReportLab PDF in place after preservation validation.

    Return a JSON-serializable check summary. Failures leave the source intact.
    ``page_tags`` order controls the structure's reading order, independently
    of drawing order; all emitted MCIDs must occur exactly once and match it.
    """
    path = Path(path).resolve(strict=True)
    source = path.read_bytes()
    source_hash = sha256(source).hexdigest()
    with pymupdf.open(path) as original:
        if not original.is_pdf or original.is_encrypted or original.is_repaired:
            raise PDFTaggingError("Expected an unencrypted, unrepaired PDF")
        if original.xref_get_key(original.pdf_catalog(), "StructTreeRoot")[0] != "null":
            raise PDFTaggingError("This PDF already has a structure tree; regenerate it before tagging")
        if not original.can_save_incrementally():
            raise PDFTaggingError("This PDF does not support an incremental metadata update")
        tags = _normalize_tags(page_tags, original.page_count)
        for page, metadata in zip(original, tags):
            expected = {tag["mcid"]: tag["role"] for tag in metadata}
            actual = _marked_content(page)
            if actual != expected:
                raise PDFTaggingError(
                    f"Page {page.number + 1}: emitted MCIDs/roles {actual!r} differ from {expected!r}")
        before = _snapshot(original)

    temporary = None
    try:
        handle, temporary = tempfile.mkstemp(prefix="." + path.stem + "-tagged-", suffix=".pdf", dir=path.parent)
        os.close(handle)
        shutil.copy2(path, temporary)
        if sha256(Path(temporary).read_bytes()).hexdigest() != source_hash:
            raise PDFTaggingError("The source PDF changed while preparing the tagging copy")
        with pymupdf.open(temporary) as document:
            tree = _add_structure(document, tags)
            document.save(temporary, incremental=True, no_new_id=True,
                          encryption=pymupdf.PDF_ENCRYPT_KEEP)
        result_bytes = Path(temporary).read_bytes()
        if not result_bytes.startswith(source):
            raise PDFTaggingError("The metadata update did not preserve the original PDF bytes")
        with pymupdf.open(temporary) as tagged:
            if tagged.is_repaired:
                raise PDFTaggingError("The tagged PDF required repair when reopened")
            _verify_structure(tagged, tree, tags)
            after = _snapshot(tagged)
            if before["pages"] != after["pages"]:
                raise PDFTaggingError("Page geometry, text, links, resources or rendering changed")
            for kind in ("objects", "streams"):
                if any(after[kind].get(xref) != value for xref, value in before[kind].items()):
                    raise PDFTaggingError(f"An existing PDF {kind[:-1]} changed")
        if sha256(path.read_bytes()).hexdigest() != source_hash:
            raise PDFTaggingError("The source PDF changed during tagging; replacement cancelled")
        os.replace(temporary, path)
        temporary = None
    finally:
        if temporary is not None:
            Path(temporary).unlink(missing_ok=True)

    roles = Counter(tag["role"] for page in tags for tag in page)
    return {
        "path": str(path), "pages": len(tags), "language": "ko-KR", "marked": True,
        "structure": "StructTreeRoot/Document/Sect/semantic elements", "parentTreeVerified": True,
        "mcidRolesVerified": True, "readingOrderVerified": True,
        "tagCount": sum(roles.values()), "roles": dict(sorted(roles.items())),
        "figureAltCount": roles["Figure"], "renderDpi": 72,
        "preservation": {"originalBytesAsPrefix": True, "pageCount": True,
                         "pageGeometry": True, "renderPixels": True, "extractedText": True,
                         "links": True, "fonts": True, "images": True,
                         "originalObjectsUnchanged": len(before["objects"]),
                         "originalStreamsUnchanged": len(before["streams"])},
        "sha256Before": source_hash, "sha256After": sha256(result_bytes).hexdigest(),
        "certification": "PDF/UA and assistive-technology conformance not assessed",
    }
