import { defineConfig } from 'vite';
import { resolve } from 'node:path';

export default defineConfig({
  base: '/admin/',
  build: {
    outDir: resolve(__dirname, '../ConsoleHost/wwwroot/admin'),
    emptyOutDir: true,
  },
});
