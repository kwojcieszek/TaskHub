// Sets the release version to today's date and the number of the release on that day, e.g. 2026.10.28.1.
import { readFileSync, writeFileSync } from 'node:fs';

const versionFile = new URL('../version.json', import.meta.url);

const pad = (value) => String(value).padStart(2, '0');
const now = new Date();
const today = `${now.getFullYear()}.${pad(now.getMonth() + 1)}.${pad(now.getDate())}`;

const current = JSON.parse(readFileSync(versionFile, 'utf8')).version ?? '';
const lastDot = current.lastIndexOf('.');
const currentDate = current.slice(0, lastDot);
const currentRelease = Number(current.slice(lastDot + 1)) || 0;

const release = currentDate === today ? currentRelease + 1 : 1;
const version = `${today}.${release}`;

writeFileSync(versionFile, `${JSON.stringify({ version }, null, 2)}\n`);
console.log(`Version ${version}`);
