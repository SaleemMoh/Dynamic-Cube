# Builds one Angular app served by an unprivileged Nginx that also reverse-proxies its API host.
#   docker build -f deploy/docker/web.Dockerfile --build-arg APP=internal -t dynamic-cube/web-internal .

FROM node:24-alpine AS build
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
ARG APP
RUN npx ng build "$APP" --configuration production

FROM nginxinc/nginx-unprivileged:1.29-alpine AS runtime
ARG APP
COPY --from=build /web/dist/${APP}/browser /usr/share/nginx/html
COPY deploy/docker/nginx/spa.conf.template /etc/nginx/templates/default.conf.template
# Overridden per app in docker-compose / Helm values.
ENV API_UPSTREAM=http://internal-api:8080
EXPOSE 8080
