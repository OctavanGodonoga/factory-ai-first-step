# syntax=docker/dockerfile:1
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS final
COPY --from=build /app/dist/talent-map-client/browser /usr/share/nginx/html
EXPOSE 80
