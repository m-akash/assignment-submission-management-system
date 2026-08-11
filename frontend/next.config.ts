import type { NextConfig } from "next";

// `output: "standalone"` tells Next.js to bundle only the files needed to run the
// server into `.next/standalone`, so the production Docker image doesn't have to
// ship the full node_modules tree. The runtime stage of the Dockerfile copies this
// standalone dir + `.next/static` + `public` and runs `node server.js` with no
// `npm install` at runtime.
const nextConfig: NextConfig = {
  output: "standalone",
};

export default nextConfig;
