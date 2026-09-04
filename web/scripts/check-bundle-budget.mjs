import { readdir, readFile } from 'node:fs/promises';
import { extname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { gzipSync } from 'node:zlib';

const distDirectory = fileURLToPath(new URL('../dist/', import.meta.url));
const budgets = {
  '.css': 5 * 1024,
  '.js': 55 * 1024,
};

async function listFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = await Promise.all(entries.map((entry) => {
    const path = join(directory, entry.name);
    return entry.isDirectory() ? listFiles(path) : path;
  }));
  return files.flat().sort();
}

function formatKilobytes(bytes) {
  return `${(bytes / 1024).toFixed(2)} kB`;
}

const files = await listFiles(distDirectory);
let failed = false;

for (const [extension, budget] of Object.entries(budgets)) {
  const matchingFiles = files.filter((file) => extname(file) === extension);
  if (matchingFiles.length === 0) {
    console.error(`Bundle budget check failed: no ${extension} assets found in dist.`);
    failed = true;
    continue;
  }

  const compressedSizes = await Promise.all(matchingFiles.map(async (file) => {
    const content = await readFile(file);
    return gzipSync(content, { level: 9 }).byteLength;
  }));
  const total = compressedSizes.reduce((sum, size) => sum + size, 0);
  const status = total <= budget ? 'PASS' : 'FAIL';
  console.log(`${status} ${extension}: ${formatKilobytes(total)} gzip / ${formatKilobytes(budget)} budget`);
  failed ||= total > budget;
}

if (failed) {
  process.exitCode = 1;
}
