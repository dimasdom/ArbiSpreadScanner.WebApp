# Changelog

## [1.1.0](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/compare/v1.0.0...v1.1.0) (2026-08-11)


### Features

* add breadcrumbs navigation to web client ([693d198](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/693d198d14682c354c07c1bd0cf9fe57b8b2dfe9))
* add robots.txt for web client ([bcc8e64](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/bcc8e644998856dffd7a5e33160abc54aede190b))


### Bug Fixes

* prevent home page from rendering invisible after refresh, add themed 404 page ([75727a8](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/75727a84b98647251e944f396defa9bd242f5748))

## 1.0.0 (2026-08-10)


### Features

* add AI assistant chat widget, MCP token page, and spread recommendations ([c4f210f](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/c4f210fb8a47db4d7dec6465881d278f6c3d3fb4))
* add dark theme ([0fd2497](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/0fd24971c5fc5904726b563d88d7bdf795e858e7))
* add localized auth loading overlay during session check ([88703b9](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/88703b9ada59b45bb12947fad93bc10ab66e1917))
* add opentelemetry ([b58acc9](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/b58acc9496298813a592cd0272256b1fce07d795))
* add test suites, error handling infra, and Grafana provisioning ([0dc860a](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/0dc860a011d4ec0c02fe12fd9d709f1c2c11ce82))
* flag for logging ([9d579ea](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/9d579ea5c4ce7f4bbbd4baf7a5237194064b55a3))
* localization ([fd6c37f](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/fd6c37fb1dd431332c576d91bb6eaa3bf29065ff))
* migrate authentication to Keycloak OIDC ([b68f567](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/b68f567ccadcba0f4ce0fd8f8335b3e060d3291a))
* navbar dissapearence ([89b6679](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/89b6679e837b579685539fbfd60b78f114d86f64))
* proxies for services ([94521f2](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/94521f2a1b95c783416bd038570bdcad0f8a486a))
* verbose logs ([c604ec0](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/c604ec0aba9a549352f75d4f84418997a66e954b))
* verbose logs ([b56d2e5](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/b56d2e5f716dca0aeb3b8467c0f55ea86500ba09))


### Bug Fixes

* align spread page layout width and make chart fill container ([0c9e100](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/0c9e10094b3a91f990709dd73d6b23cb436c8865))
* chatbot markdown ([b7e4cb1](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/b7e4cb159a4eae1ce5ab01dbaac376b35d934765))
* **ci:** address SonarQube findings on release.yml ([b7e079b](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/b7e079b2ba3508cdec3c4be05409bd086da2c47f))
* **ci:** auto-merge step failed on every release PR ([f81d660](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/f81d660bae6eed5111e39db61f256ab05d8c61f8))
* **ci:** fix deploy workflow's missing Keycloak realm-export staging ([1c2e8e5](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/1c2e8e5ca23f39a09461590be1a945c1cfe65889))
* **ci:** pass VITE_OIDC_* build args to web-client image ([747ae44](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/747ae4414b6f0dd4465d370a8ee8e2be558cecc0))
* client tests result ([730bc1b](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/730bc1b78e76075ba1747176db502b2262623d43))
* correct RabbitMQ ack timing, add idempotent dedupe and DLQ ([7f8ce85](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/7f8ce85712eb69faa9b828815b399d48a948258e))
* deploy ([2ffb2b2](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/2ffb2b2c597a9b4871bf9b274ba73172a0ce3791))
* deploy pipeline ([35df6ef](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/35df6ef91696d915105cccccdc0a22bf6b826866))
* deploy sonarqube coverage ([d4b0ac0](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/d4b0ac07163fe129f3e837030b9603e028ccc7c0))
* exclude funding rate from Funding-spread cost check in spread analysis ([7bf0d6c](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/7bf0d6c4f278f3ce0efb80d21dd8e243ccb874b4))
* fetch monorepo Keycloak realm export in CI for integration tests ([b82359f](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/b82359ff311f2e95bc05dc41ba08fe052de03dbc))
* integration tests pipeline ([7fb1188](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/7fb118899f2d2cfa4a6506aef86e8f76049d3b80))
* load tests ([674d1be](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/674d1be6b6897e2eb5bffbbdf167f556bd932717))
* mongo race condition ([be7e671](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/be7e671983c7cbdfb241ca750b988c39d190d4ed))
* pin monorepo checkout ref for Keycloak realm export in CI ([f52b241](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/f52b24153037b342f060c57dfa669cdef1cb2b51))
* pipeline ([e441fc4](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/e441fc4ba50beaa535e64519ea0a146bab6ed545))
* pipeline ([75756f9](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/75756f9ad2484b130ad72b381bdf07c06ea51734))
* pipeline build ([fdc6455](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/fdc645525f8403c4ff7e577c5a63ae9b439b3872))
* pipeline tests db ([47b01cc](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/47b01ccf7b192b1882daaf7e0b8d65d11eb5cbfa))
* repin deploy-service.yml to pick up client test publish fix ([210d3dd](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/210d3ddf724ac71ea30145c5fabb287dae3cb85b))
* repin deploy-service.yml to the corrected working-directory fix ([9af1940](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/9af194076860d3786652bd16a25cd61d731d7a47))
* resolve SonarQube new-code findings on the Keycloak OIDC migration PR ([46c5cd9](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/46c5cd9f231fd7805828f0179c17dbad50b8b4d2))
* serialize integration test collections to stop flaky IHost startup failures ([2292ed5](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/2292ed5aecfacd75bce8adb736ddebefd0f68894))
* use consistent no-space percent formatting in spread analysis reasons ([d2bcd27](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/d2bcd27a2e94ab778f9ad01dc49d85fab511404b))
* use consistent spinner style on SubscriptionPage ([f2c4546](https://github.com/dimasdom/ArbiSpreadScanner.WebApp/commit/f2c45463917ef1daece8da17ae7bb2045cbb62ff))
