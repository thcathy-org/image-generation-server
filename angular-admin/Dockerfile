# build
FROM node:18-alpine AS build

ARG base_href

WORKDIR /usr/src/app

COPY package.json package-lock.json ./

RUN npm install

COPY . .
RUN npm run build --production -- --base-href=$base_href

# deploy
FROM nginx:1.23.3-alpine
COPY nginx.conf /etc/nginx/nginx.conf
COPY --from=build /usr/src/app/dist/* /usr/share/nginx/html
