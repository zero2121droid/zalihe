#!/usr/bin/env bash
# Turns the local test shop into "Nit", a demo clothing shop for showing Zalihe to clients:
# Serbian (Latin) texts, own colors and fonts, clothing with photos and sizes, home page, menu,
# shipping and cash on delivery. Run setup.sh first. Safe to run again.
#
#   docker compose run --rm wpcli bash /scripts/demo.sh
#
# The coffee test products stay (Zalihe can still import them) but are hidden from the shop.
# Local development only.
set -euo pipefail

if ! wp plugin is-active woocommerce 2>/dev/null; then
  echo "Run setup.sh first." >&2
  exit 1
fi

wp option update blogname "Nit" >/dev/null
wp option update blogdescription "Odeća koja traje" >/dev/null
wp option update woocommerce_coming_soon no >/dev/null
wp option update woocommerce_enable_reviews no >/dev/null
wp option update timezone_string Europe/Belgrade >/dev/null
# "1.990 RSD" instead of "RSD1.990,00", as Serbian shops show prices.
wp option update woocommerce_currency_pos right_space >/dev/null
wp option update woocommerce_price_num_decimals 0 >/dev/null
# Purchase prices ("Cost of goods sold"), which Zalihe imports as items' purchase price.
wp option update woocommerce_feature_cost_of_goods_sold_enabled yes >/dev/null

mkdir -p wp-content/mu-plugins
cp /scripts/nit-demo.php wp-content/mu-plugins/

# WordPress has only Cyrillic Serbian; nit-demo.php shows it in Latin.
wp language core is-installed sr_RS || wp language core install sr_RS >/dev/null
[ "$(wp option get WPLANG)" = sr_RS ] || wp site switch-language sr_RS >/dev/null
wp language plugin install woocommerce sr_RS >/dev/null
wp language theme install twentytwentyfive sr_RS >/dev/null
# Admin stays in English for the admin user; the shop is in Serbian.
wp user meta update admin locale en_US >/dev/null

echo "Catalog:"
wp eval-file /scripts/demo-catalog.php
wp rewrite flush --hard >/dev/null

echo
echo "Demo shop: http://localhost:8080  (admin / admin)"
