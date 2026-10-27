# Wee Woo Run — published builds

GitHub Pages serves the root of the `gh-pages` branch. Game source remains on `main`.

- `beta-progress/`: Unity 6000.3.23f1 WebGL release build, source commit `1b07a54`.
- Original alpha: https://drewcarroll.github.io/wee-woo-run/alpha/ (hosted separately, unchanged).

## Preserve assignment builds

Publish each future milestone in a new folder. Do not replace previous milestone folders.
Build all enabled scenes, beginning with Env_City. Use Gzip compression with decompression fallback enabled for GitHub Pages. Commit the actual build files here, not Git LFS pointers. Keep `.nojekyll` at this branch's root.

Analytics build tag for this release: `beta-progress-1b07a54`.
