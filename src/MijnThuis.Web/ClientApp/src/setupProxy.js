const { createProxyMiddleware } = require('http-proxy-middleware');
const { env } = require('process');

const target = env.services__api__https__0 || env.services__api__http__0 || 'http://localhost:64181';

const context =  [
  "/weatherforecast",
];

module.exports = function(app) {
  const appProxy = createProxyMiddleware(context, {
    proxyTimeout: 10000,
    target: target,
    secure: false,
    headers: {
      Connection: 'Keep-Alive'
    }
  });

  app.use(appProxy);
};
