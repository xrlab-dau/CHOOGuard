// 설치된 pi-subagents의 실제 parser로 프로젝트 프로필을 검사한다. 네트워크/자식 실행 없음.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { stripTypeScriptTypes } from 'node:module';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const packageRoot = resolve(root, '.pi/npm/node_modules/pi-subagents');
const version = JSON.parse(readFileSync(resolve(packageRoot, 'package.json'), 'utf8')).version;
assert.equal(version, '0.65.1', '고정 pi-subagents 패키지를 설치한 환경에서 실행한다.');
const parserSource = readFileSync(resolve(packageRoot, 'src/agents/frontmatter.ts'), 'utf8');
const parser = await import('data:text/javascript,' + encodeURIComponent(
  stripTypeScriptTypes(parserSource, { mode: 'strip' }),
));

const expected = {
  'adversarial-reviewer': ['read', 'grep', 'find', 'ls'],
  'doc-reviser': ['read', 'edit', 'write', 'grep', 'find', 'ls'],
  implementer: ['read', 'bash', 'edit', 'write', 'grep', 'find', 'ls'],
  'safety-auditor': ['read', 'grep', 'find', 'ls'],
  scout: ['read', 'grep', 'find', 'ls'],
  'spec-reviewer': ['read', 'grep', 'find', 'ls'],
  verifier: ['read', 'bash', 'grep', 'find', 'ls'],
};

for (const [name, tools] of Object.entries(expected)) {
  test(`${name}: intended tools and no spurious skill names`, () => {
    const source = readFileSync(resolve(root, `.pi/agents/${name}.md`), 'utf8');
    const { frontmatter } = parser.parseFrontmatter(source);
    assert.deepEqual(parser.parseFrontmatterList(frontmatter.tools), tools);
    assert.deepEqual(parser.parseFrontmatterList(frontmatter.skills) ?? [], []);
  });
}
