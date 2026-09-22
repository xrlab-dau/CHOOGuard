# Busan building-height source evidence

This folder holds a bounded extract of TUM GlobalBuildingAtlas (GBA.Height) for review. It is a machine-learning prediction, not surveyed or current as-built height. Dataset license: CC BY-NC 4.0. No publication or upload is performed by these scripts.

Primary sources:
- https://github.com/zhu-xlab/GlobalBuildingAtlas
- https://mediatum.ub.tum.de/1782307
- https://essd.copernicus.org/articles/17/6647/2025/
- https://github.com/zhu-xlab/GlobalBuildingAtlas/blob/main/make_lod1/main.py

The paper describes 2019 PlanetScope imagery, supplemented by 2018 imagery where necessary. Exact tile acquisition dates are not encoded in the TIFF. The native grid and scaling are read from the acquired raster. The TIFF lacks a band-unit tag; metre semantics come from the publisher's height product and direct unscaled LoD1 extraction code.

`gba-acquisition-receipt.json` records strict HTTP 206 / Content-Range checks, member offsets and lengths, compressed and TIFF hashes, and ZIP CRC verification. The archive is never downloaded in full. A conservative 32 MB budget charge accounts for the interrupted first stream; it is not a measured byte count. The final acquisition uses eight concurrent bounded ranges of one exact member.

Run with the project-local Python environment:

```sh
.tools/building-data/.venv/bin/python art/world/acquire_busan_gba_height.py
.tools/building-data/.venv/bin/python art/world/prepare_busan_gba_height_stats.py
```

The separate lookup targets only 665 existing footprints without height or floor-count tags. A reviewable estimate needs at least four valid pixel centres within the original OSM polygon and at least 80% valid coverage. Its maximum follows the publisher's building-height aggregation concept; median and other statistics remain available for sensitivity review. It uses centre inclusion rather than the publisher code's all-touched inclusion to reduce edge contamination. Roof features, alignment errors, stale imagery and model errors can still affect estimates. Pixel standard deviation is spatial spread, not calibrated prediction uncertainty. The separate GBA variance tile has not been acquired.

Existing OSM height and floor-count tags are never overwritten. No Unity Assets or builder changes are made here. A reviewable estimate is not automatic product acceptance.
