import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import fs from 'fs';
import path from 'path';
import child_process from 'child_process';
import { env } from 'process';
import tailwindcss from '@tailwindcss/vite'
const baseFolder =
    env.APPDATA !== undefined && env.APPDATA !== ''
        ? `${env.APPDATA}/ASP.NET/https`
        : `${env.HOME}/.aspnet/https`;

const certificateName = "ArbiScannerWeb.Client";
const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

if (!fs.existsSync(baseFolder)) {
    fs.mkdirSync(baseFolder, { recursive: true });
}

if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
    if (0 !== child_process.spawnSync('dotnet', [
        'dev-certs',
        'https',
        '--export-path',
        certFilePath,
        '--format',
        'Pem',
        '--no-password',
    ], { stdio: 'inherit', }).status) {
        throw new Error("Could not create certificate.");
    }
}

function getDevServerTarget(): string {
    if (env.ASPNETCORE_HTTPS_PORT) {
        return `https://localhost:${env.ASPNETCORE_HTTPS_PORT}`;
    }
    if (env.ASPNETCORE_URLS) {
        return env.ASPNETCORE_URLS.split(';')[0];
    }
    return 'https://localhost:7274';
}

const target = getDevServerTarget();
// ArbiScanner.AiAssistant runs as its own local process (see
// ArbiScanner.AiAssistant.Api/Properties/launchSettings.json), not part of this
// dev-cert/port dance, so it gets its own target rather than reusing `target`.
const aiAssistantTarget = env.AI_ASSISTANT_URL ?? 'http://localhost:5272';

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin(),tailwindcss()],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    server: {
        proxy: {
            '^/api': {
                target,
                secure: false,
            },
            '^/hubs': {
                target,
                secure: false,
                ws: true
            },
            '^/ai-hub': {
                target: aiAssistantTarget,
                secure: false,
                ws: true
            }
        },
        port: 12321,
        https: {
            key: fs.readFileSync(keyFilePath),
            cert: fs.readFileSync(certFilePath),
        }
    }
})
