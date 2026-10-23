// Runs the dev server on https in the local network (npm run dev:https), so a phone can open the app
// and use its camera, which browsers allow only on secure pages. The certificate is self-signed and made
// here with openssl for localhost and this computer's addresses; it is made again when an address changes.
// The phone warns once that the connection isn't private: accept it to go on. It has a port of its own
// (5174), so it runs next to the usual npm run dev.
import { execFileSync, spawn } from 'node:child_process'
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { networkInterfaces } from 'node:os'
import { fileURLToPath } from 'node:url'

const PORT = 5174
const dir = new URL('../.cert/', import.meta.url)
const addresses = Object.values(networkInterfaces())
  .flat()
  .filter((a) => a && a.family === 'IPv4' && !a.internal)
  .map((a) => a.address)
const names = ['DNS:localhost', 'IP:127.0.0.1', ...addresses.map((a) => `IP:${a}`)].join(',')

const hostsFile = new URL('hosts.txt', dir)
if (!existsSync(hostsFile) || readFileSync(hostsFile, 'utf8') !== names) {
  mkdirSync(dir, { recursive: true })
  execFileSync('openssl', [
    'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-days', '397',
    '-keyout', fileURLToPath(new URL('key.pem', dir)),
    '-out', fileURLToPath(new URL('cert.pem', dir)),
    '-subj', '/CN=Lagerko dev',
    '-addext', `subjectAltName=${names}`,
    '-addext', 'extendedKeyUsage=serverAuth',
  ], { stdio: 'ignore' })
  writeFileSync(hostsFile, names)
}

console.log('\nOn the phone (same Wi-Fi):')
for (const address of addresses) console.log(`  https://${address}:${PORT}`)
console.log('')

spawn('npx', ['vite', '--host', '--port', String(PORT), '--strictPort'], { stdio: 'inherit', env: { ...process.env, DEV_HTTPS: '1' } }).on('exit', (code) => process.exit(code ?? 0))
