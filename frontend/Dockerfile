# Stage 1: Build stage
FROM node:20-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .

# Support build-time variables for client bundle baking
ARG NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ENV NEXT_TELEMETRY_DISABLED=1

# next.config.ts sets output: "standalone", so the build produces a self-contained
# server in .next/standalone that needs no node_modules at runtime.
RUN npm run build

# Stage 2: Runtime stage — lean, no node_modules, runs the standalone server.
FROM node:20-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000
ENV HOSTNAME="0.0.0.0"

RUN addgroup --system --gid 1001 nodejs
RUN adduser --system --uid 1001 nextjs

# The standalone output does NOT copy public/ or .next/static by itself — both must
# be layered on top manually. See: https://nextjs.org/docs/app/api-reference/config/next-config-js/output
COPY --from=builder /app/public ./public
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static

USER nextjs

EXPOSE 3000

# Standalone server entrypoint (no `npm start` — saves a process layer too).
CMD ["node", "server.js"]
