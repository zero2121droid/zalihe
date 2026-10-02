#!/usr/bin/env bash
# Sets up the local WooCommerce test shop: WordPress, WooCommerce, test products
# (simple and variable) and a REST API key for Zalihe. Safe to run again.
#
#   docker compose run --rm wpcli bash /scripts/setup.sh
#
# Shop: http://localhost:8080 (admin / admin). Local development only.
set -euo pipefail

if ! wp core is-installed 2>/dev/null; then
  wp core install --url=http://localhost:8080 --title="Zalihe test shop" \
    --admin_user=admin --admin_password=admin --admin_email=admin@example.com --skip-email
fi

# Pretty permalinks are needed for /wp-json/ URLs. WP-CLI only writes .htaccess when told
# that Apache has mod_rewrite (the WordPress image enables it).
printf 'apache_modules:\n  - mod_rewrite\n' > wp-cli.yml
wp rewrite structure '/%postname%/' --hard >/dev/null

mkdir -p wp-content/mu-plugins
cp /scripts/zalihe-local-https.php wp-content/mu-plugins/

if ! wp plugin is-active woocommerce 2>/dev/null; then
  wp plugin install woocommerce --activate
fi

# Serbian shop settings; skip the onboarding wizard.
wp option update woocommerce_currency RSD >/dev/null
wp option update woocommerce_default_country RS >/dev/null
wp option update woocommerce_price_decimal_sep ',' >/dev/null
wp option update woocommerce_price_thousand_sep '.' >/dev/null
wp option update woocommerce_manage_stock yes >/dev/null
wp option update woocommerce_onboarding_profile '{"skipped":true}' --format=json >/dev/null

has_sku() { [ -n "$(wp post list --post_type=product,product_variation --meta_key=_sku --meta_value="$1" --format=ids)" ]; }

simple() { # name sku price stock
  has_sku "$2" && return
  wp wc product create --user=admin --name="$1" --sku="$2" --regular_price="$3" \
    --manage_stock=true --stock_quantity="$4" --porcelain >/dev/null
  echo "  + $1 ($2)"
}

echo "Products:"
simple "Kafa Etiopija Yirgacheffe 250 g" KF-ETI-250 1250 24
simple "Kafa Kolumbija Huila 250 g" KF-KOL-250 1150 31
simple "Kafa Brazil Santos 1 kg" KF-BRA-1000 3400 0
simple "Ručni mlin za kafu" ML-RUC-01 9900 6
simple "Filter papir V60, 100 kom" FP-V60-100 590 6
simple "Proizvod samo u prodavnici" WOO-ONLY-1 500 3

# A variable product: each variation (size x color) is a separate item in Zalihe.
if ! has_sku MAJ-BASIC; then
  parent=$(wp wc product create --user=admin --name="Majica basic" --type=variable --sku=MAJ-BASIC \
    --attributes='[{"name":"Veličina","options":["M","L"],"variation":true,"visible":true},{"name":"Boja","options":["Siva","Bela"],"variation":true,"visible":true}]' \
    --porcelain)
  for size in M L; do
    for color in Siva Bela; do
      code=$([ "$color" = Siva ] && echo SI || echo BE)
      wp wc product_variation create "$parent" --user=admin --sku="MAJ-$size-$code" --regular_price=1990 \
        --manage_stock=true --stock_quantity=5 \
        --attributes="[{\"name\":\"Veličina\",\"option\":\"$size\"},{\"name\":\"Boja\",\"option\":\"$color\"}]" \
        --porcelain >/dev/null
    done
  done
  echo "  + Majica basic (MAJ-BASIC, 4 variations)"
fi

# A fresh read/write API key for Zalihe on every run (the secret can only be shown once).
keys=$(wp eval '
  global $wpdb;
  $table = $wpdb->prefix . "woocommerce_api_keys";
  $wpdb->delete($table, ["description" => "Zalihe (lokalno)"]);
  $user = get_user_by("login", "admin");
  $key = "ck_" . wc_rand_hash();
  $secret = "cs_" . wc_rand_hash();
  $wpdb->insert($table, [
    "user_id" => $user->ID, "description" => "Zalihe (lokalno)", "permissions" => "read_write",
    "consumer_key" => wc_api_hash($key), "consumer_secret" => $secret, "truncated_key" => substr($key, -7),
  ]);
  echo $key . " " . $secret;
')

echo
echo "Shop:            http://localhost:8080  (admin / admin)"
echo "Consumer key:    ${keys% *}"
echo "Consumer secret: ${keys#* }"
