<?php
// Helpers shared by the demo shop catalogs (nit-catalog.php, zrno-catalog.php), loaded with require_once.
// Everything here is safe to run again: existing images, products and pages are reused.

require_once ABSPATH . 'wp-admin/includes/media.php';
require_once ABSPATH . 'wp-admin/includes/file.php';
require_once ABSPATH . 'wp-admin/includes/image.php';

// The local shop cannot send email (WooCommerce warns about low stock while products are created).
add_filter('pre_wp_mail', '__return_false');

// A free Pexels photo (https://www.pexels.com/license/), downloaded on the first run. The meta key keeps
// the name it had when only the "Nit" shop existed, so that shop's photos are not downloaded again.
function demo_image(int $pexels_id, string $title): int
{
    $existing = get_posts(['post_type' => 'attachment', 'meta_key' => '_nit_pexels_id', 'meta_value' => $pexels_id, 'fields' => 'ids']);
    if ($existing) {
        return $existing[0];
    }
    $url = "https://images.pexels.com/photos/$pexels_id/pexels-photo-$pexels_id.jpeg?auto=compress&cs=tinysrgb&w=1400";
    $id = media_sideload_image($url, 0, $title, 'id');
    if (is_wp_error($id)) {
        WP_CLI::warning("Photo $pexels_id: " . $id->get_error_message());
        return 0;
    }
    update_post_meta($id, '_nit_pexels_id', $pexels_id);
    update_post_meta($id, '_wp_attachment_image_alt', $title);
    return $id;
}

// Categories: [slug => [name, Pexels photo id, description]] in, [slug => term id] out.
function demo_categories(array $categories): array
{
    $ids = [];
    foreach ($categories as $slug => [$name, $photo, $description]) {
        $term = get_term_by('slug', $slug, 'product_cat');
        $term_id = $term ? $term->term_id : wp_insert_term($name, 'product_cat', ['slug' => $slug, 'description' => $description])['term_id'];
        if (!get_term_meta($term_id, 'thumbnail_id', true)) {
            update_term_meta($term_id, 'thumbnail_id', demo_image($photo, $name));
        }
        $ids[$slug] = $term_id;
    }
    return $ids;
}

// Varied stock, so Zalihe shows normal, low and empty items after the import.
function demo_next_stock(): int
{
    static $pattern = [14, 8, 3, 0, 11, 22, 6, 2, 17, 9, 0, 25, 5, 12];
    static $index = 0;
    return $pattern[$index++ % count($pattern)];
}

// A new product, skipped when its SKU exists. Each variation is a separate item (SKU) in Zalihe.
// $attributes: ['Veličina' => ['S', 'M']]; empty for a simple product.
function demo_product(string $sku, string $name, int $category_id, int $price, int $photo, bool $featured,
    array $attributes, string $short, string $description): void
{
    if (wc_get_product_id_by_sku($sku)) {
        return;
    }
    $product = $attributes ? new WC_Product_Variable() : new WC_Product_Simple();
    $product->set_name($name);
    $product->set_sku($sku);
    $product->set_short_description($short);
    $product->set_description($description);
    $product->set_category_ids([$category_id]);
    $product->set_featured($featured);
    $product->set_image_id(demo_image($photo, $name));
    $product->set_reviews_allowed(false);

    if (!$attributes) {
        $product->set_regular_price((string) $price);
        $product->set_manage_stock(true);
        $product->set_stock_quantity(demo_next_stock());
        $product->save();
        WP_CLI::log("  + $name ($sku)");
        return;
    }

    $product_attributes = [];
    foreach ($attributes as $attribute_name => $options) {
        $attribute = new WC_Product_Attribute();
        $attribute->set_name($attribute_name);
        $attribute->set_options($options);
        $attribute->set_visible(true);
        $attribute->set_variation(true);
        $product_attributes[] = $attribute;
    }
    $product->set_attributes($product_attributes);
    $parent_id = $product->save();

    // Every combination of options: [['Boja' => 'Crna', 'Veličina' => 'M'], ...].
    $combinations = [[]];
    foreach ($attributes as $attribute_name => $options) {
        $next = [];
        foreach ($combinations as $combination) {
            foreach ($options as $option) {
                $next[] = $combination + [$attribute_name => $option];
            }
        }
        $combinations = $next;
    }
    foreach ($combinations as $combination) {
        $variation = new WC_Product_Variation();
        $variation->set_parent_id($parent_id);
        $variation->set_attributes(array_combine(array_map('sanitize_title', array_keys($combination)), array_values($combination)));
        $variation->set_sku($sku . '-' . strtoupper(implode('-', array_map(fn ($o) => sanitize_title($o), $combination))));
        $variation->set_regular_price((string) $price);
        $variation->set_manage_stock(true);
        $variation->set_stock_quantity(demo_next_stock());
        $variation->save();
    }
    WC_Product_Variable::sync($parent_id);
    WP_CLI::log("  + $name ($sku, " . count($combinations) . " variations)");
}

// Purchase price ("Cost of goods sold"): about 45% of the sale price, rounded to tens. Set on existing
// products too; variations without their own cost use their product's. $prices: [sku => sale price].
function demo_set_costs(array $prices): void
{
    foreach ($prices as $sku => $price) {
        $product = wc_get_product(wc_get_product_id_by_sku($sku));
        $product->set_cogs_value(round($price * 0.45, -1));
        $product->save();
    }
}

// Barcodes ("GTIN, UPC, EAN ili ISBN", WooCommerce 9.2+) on every simple product and variation that has
// none, so Zalihe can show finding an item by its barcode. EAN-13 from the 200 prefix, which GS1 keeps
// for in-store use, so no real company's barcode is borrowed: 200 + the product ID + a check digit.
function demo_ean(int $id): string
{
    $digits = '200' . str_pad((string) $id, 9, '0', STR_PAD_LEFT);
    $sum = 0;
    foreach (str_split($digits) as $i => $digit) {
        $sum += (int) $digit * ($i % 2 === 0 ? 1 : 3);
    }
    return $digits . ((10 - $sum % 10) % 10);
}

function demo_add_barcodes(array $skus): void
{
    $added = 0;
    foreach ($skus as $sku) {
        $product = wc_get_product(wc_get_product_id_by_sku($sku));
        $targets = $product->is_type('variable') ? array_map('wc_get_product', $product->get_children()) : [$product];
        foreach ($targets as $target) {
            if (method_exists($target, 'set_global_unique_id') && !$target->get_global_unique_id()) {
                $target->set_global_unique_id(demo_ean($target->get_id()));
                $target->save();
                $added++;
            }
        }
    }
    WP_CLI::log("  barcodes added: $added");
}

// Products outside the shop's categories (the test products from setup.sh and more-products.sh) stay,
// so Zalihe can still import them, but are hidden from the shop.
function demo_hide_other_products(array $category_ids): void
{
    $hidden = 0;
    foreach (wc_get_products(['limit' => -1, 'status' => 'publish', 'return' => 'objects']) as $product) {
        if (!array_intersect($product->get_category_ids(), $category_ids) && $product->get_catalog_visibility() !== 'hidden') {
            $product->set_catalog_visibility('hidden');
            $product->save();
            $hidden++;
        }
    }
    if ($hidden) {
        WP_CLI::log("Hidden $hidden test products from the shop.");
    }
}

// Serbian titles and slugs for the WooCommerce pages, no sample content.
function demo_serbian_pages(): void
{
    $rename = [
        'woocommerce_shop_page_id' => ['Prodavnica', 'prodavnica'],
        'woocommerce_cart_page_id' => ['Korpa', 'korpa'],
        'woocommerce_checkout_page_id' => ['Plaćanje', 'placanje'],
        'woocommerce_myaccount_page_id' => ['Moj nalog', 'moj-nalog'],
        'woocommerce_refund_returns_page_id' => ['Povraćaj i reklamacije', 'povracaj'],
        'wp_page_for_privacy_policy' => ['Politika privatnosti', 'politika-privatnosti'],
    ];
    foreach ($rename as $option => [$title, $slug]) {
        if ($page_id = (int) get_option($option)) {
            wp_update_post(['ID' => $page_id, 'post_title' => $title, 'post_name' => $slug]);
        }
    }
    foreach ([get_page_by_path('sample-page'), get_page_by_path('hello-world', OBJECT, 'post')] as $sample) {
        if ($sample) {
            wp_delete_post($sample->ID, true);
        }
    }
    update_option('woocommerce_permalinks', array_merge((array) get_option('woocommerce_permalinks', []), [
        'product_base' => '/proizvod',
        'category_base' => 'kategorija',
        'tag_base' => 'oznaka',
    ]));
}

function demo_page(string $slug, string $title, string $content, string $template = ''): int
{
    $page = get_page_by_path($slug);
    $data = ['post_type' => 'page', 'post_status' => 'publish', 'post_name' => $slug, 'post_title' => $title, 'post_content' => $content];
    $id = $page ? wp_update_post(['ID' => $page->ID] + $data) : wp_insert_post($data);
    if ($template) {
        update_post_meta($id, '_wp_page_template', $template);
    }
    return $id;
}

function demo_home_page(string $content): void
{
    $home_id = demo_page('pocetna', 'Početna', $content, 'page-no-title');
    update_option('show_on_front', 'page');
    update_option('page_on_front', $home_id);
}

// Main menu: [label => path]. The theme's navigation block uses the newest menu when it has none of its own.
function demo_menu(array $links): void
{
    $menu = '';
    foreach ($links as $label => $path) {
        $menu .= '<!-- wp:navigation-link {"label":"' . $label . '","url":"' . esc_url(home_url($path)) . '","kind":"custom"} /-->';
    }
    $existing = get_posts(['post_type' => 'wp_navigation', 'title' => 'Glavni meni', 'post_status' => 'publish', 'fields' => 'ids']);
    $data = ['post_type' => 'wp_navigation', 'post_status' => 'publish', 'post_title' => 'Glavni meni', 'post_content' => $menu];
    $existing ? wp_update_post(['ID' => $existing[0]] + $data) : wp_insert_post($data);
}

// Shipping in Serbia and cash on delivery, so a demo order goes all the way through checkout.
function demo_shipping_and_cod(int $shipping_cost, int $free_from): void
{
    $has_zone = false;
    foreach (WC_Shipping_Zones::get_zones() as $zone) {
        $has_zone = $has_zone || $zone['zone_name'] === 'Srbija';
    }
    if (!$has_zone) {
        $zone = new WC_Shipping_Zone();
        $zone->set_zone_name('Srbija');
        $zone->add_location('RS', 'country');
        $zone->save();
        $flat = $zone->add_shipping_method('flat_rate');
        update_option("woocommerce_flat_rate_{$flat}_settings", ['title' => 'Kurirska služba', 'tax_status' => 'none', 'cost' => (string) $shipping_cost]);
        $free = $zone->add_shipping_method('free_shipping');
        update_option("woocommerce_free_shipping_{$free}_settings", ['title' => 'Besplatna dostava', 'requires' => 'min_amount', 'min_amount' => (string) $free_from]);
    }
    update_option('woocommerce_cod_settings', [
        'enabled' => 'yes',
        'title' => 'Plaćanje pouzećem',
        'description' => 'Plaćate kuriru prilikom preuzimanja paketa.',
        'instructions' => 'Plaćate kuriru prilikom preuzimanja paketa.',
        'enable_for_methods' => [],
        'enable_for_virtual' => 'yes',
    ]);
}

// Block markup used by the home and info pages.
function demo_p(string $text): string
{
    return "<!-- wp:paragraph -->\n<p>$text</p>\n<!-- /wp:paragraph -->\n";
}

function demo_h(string $text, int $level = 2): string
{
    return "<!-- wp:heading {\"level\":$level} -->\n<h$level class=\"wp-block-heading\">$text</h$level>\n<!-- /wp:heading -->\n";
}
