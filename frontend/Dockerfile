# Stage 1: build React app with Vite
# VITE_API_URL è bakata nel bundle JS in fase di build. La chiave VAPID invece
# si legge a runtime da GET /push/vapid-public-key (issue #76) — non serve
# più nessun build arg per quella.
ARG VITE_API_URL=http://localhost:8080

FROM node:22-alpine AS build
ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL

WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Stage 2: serve static files with Nginx
FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
