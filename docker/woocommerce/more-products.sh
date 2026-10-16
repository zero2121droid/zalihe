#!/usr/bin/env bash
# Adds 60 more simple products to the local test shop, for trying a bigger product import.
# Run setup.sh first. Products whose SKU already exists are skipped, so it is safe to run again.
# A batch number above 1 adds another 60 with their own SKUs ("-S2") and names ("serija 2").
#
#   docker compose run --rm wpcli bash /scripts/more-products.sh      # batch 1
#   docker compose run --rm wpcli bash /scripts/more-products.sh 2    # 60 more
#
# Uses WooCommerce's PHP API in one call; "wp wc product create" per product takes about a second each.
set -euo pipefail

batch="${1:-1}"
case "$batch" in ''|*[!0-9]*) echo "Batch must be a number." >&2; exit 1 ;; esac

BATCH="$batch" wp eval '
  $batch = (int) getenv("BATCH");
  $products = [];
  $coffees = ["Etiopija Sidamo", "Kenija AA", "Gvatemala Antigua", "Kostarika Tarrazu", "Peru Cajamarca",
              "Ruanda Huye", "Honduras Marcala", "Indija Monsooned Malabar", "Sumatra Mandheling", "Espresso blend"];
  $sizes = [["250 g", "250", 1190], ["500 g", "500", 2190], ["1 kg", "1000", 3990]];
  foreach ($coffees as $i => $origin) {
    foreach ($sizes as [$label, $code, $price]) {
      $products[] = ["Kafa $origin $label", sprintf("KF-M%02d-%s", $i + 1, $code), $price + $i * 40];
    }
  }
  $teas = ["Zeleni čaj Sencha", "Crni čaj Assam", "Earl Grey", "Kamilica", "Nana", "Rooibos vanila",
           "Jasmin", "Oolong", "Matcha", "Hibiskus"];
  foreach ($teas as $i => $tea) {
    $products[] = ["$tea 50 g", sprintf("CJ-%02d-50", $i + 1), 390 + $i * 30];
    $products[] = ["$tea 100 g", sprintf("CJ-%02d-100", $i + 1), 690 + $i * 50];
  }
  $gear = [["French press 1 l", 3490], ["Moka džezva 3 šolje", 2990], ["V60 keramički", 3290],
           ["Chemex 6 šolja", 6990], ["Vaga za kafu 0,1 g", 4590], ["Kuvalo sa guščjim vratom", 7490],
           ["Tamper 58 mm", 2490], ["Šolja espresso 80 ml", 790], ["Šolja kapućino 180 ml", 990],
           ["Termos za kafu 450 ml", 2790]];
  foreach ($gear as $i => [$name, $price]) {
    $products[] = [$name, sprintf("OP-%02d", $i + 1), $price];
  }

  if ($batch > 1) {
    $products = array_map(fn ($p) => [$p[0] . " · serija $batch", $p[1] . "-S$batch", $p[2]], $products);
  }

  $added = 0;
  foreach ($products as $n => [$name, $sku, $price]) {
    if (wc_get_product_id_by_sku($sku)) continue;
    $product = new WC_Product_Simple();
    $product->set_name($name);
    $product->set_sku($sku);
    $product->set_regular_price((string) $price);
    $product->set_manage_stock(true);
    // Mostly stocked, every seventh empty and every fifth low, so statuses vary after import.
    $product->set_stock_quantity($n % 7 === 0 ? 0 : ($n % 5 === 0 ? 3 : 12 + ($n * 7) % 40));
    $product->save();
    $added++;
  }
  echo "Added $added products (" . (count($products) - $added) . " already there).\n";
'
