<?php
// Catalog, pages, menu, shipping and payment of the "Nit" demo clothing shop.
// Run through demo.sh ("wp eval-file"). Safe to run again: existing products (by SKU), terms,
// images and pages are reused, and the home page, menu and settings are rewritten.
// Photos are free Pexels photos (https://www.pexels.com/license/), downloaded on the first run.

require_once ABSPATH . 'wp-admin/includes/media.php';
require_once ABSPATH . 'wp-admin/includes/file.php';
require_once ABSPATH . 'wp-admin/includes/image.php';

// The local shop cannot send email (WooCommerce warns about low stock while products are created).
add_filter('pre_wp_mail', '__return_false');

const NIT_SIZES = ['S', 'M', 'L', 'XL'];
const NIT_JEANS = ['30', '32', '34', '36'];

$categories = [
    // slug => [name, Pexels photo id, description]
    'majice' => ['Majice', 34156906, 'Pamučne majice za svaki dan.'],
    'duksevi-i-dzemperi' => ['Duksevi i džemperi', 5710046, 'Topli slojevi od pamuka i vune.'],
    'farmerke' => ['Farmerke', 10133274, 'Teksas koji se nosi godinama.'],
    'jakne' => ['Jakne', 7671163, 'Jakne i kaputi za prelazni period i zimu.'],
    'aksesoari' => ['Aksesoari', 9594141, 'Kape, čarape i torbe.'],
];

$products = [
    // [sku, name, category, price, Pexels photo id, featured, variation attributes, short description]
    ['NIT-MAJ-BELA', 'Majica Basic, bela', 'majice', 1890, 12025472, true, ['Veličina' => NIT_SIZES],
        'Klasična majica od 100% organskog pamuka, 180 g/m². Ravan kroj, ojačan okovratnik.'],
    ['NIT-MAJ-DUO', 'Majica pamučna', 'majice', 2190, 34156906, false, ['Boja' => ['Crna', 'Mint'], 'Veličina' => ['M', 'L']],
        'Mekana pamučna majica u dve boje. Pere se na 30 °C i ne gubi oblik.'],
    ['NIT-DUK-PLAVI', 'Duks sa kapuljačom, plavi', 'duksevi-i-dzemperi', 4490, 37468338, true, ['Veličina' => NIT_SIZES],
        'Duks od četkanog pamuka sa kapuljačom i džepom napred. Topao, a lagan.'],
    ['NIT-DZE-PRUG', 'Džemper na pruge', 'duksevi-i-dzemperi', 5990, 45982, false, ['Veličina' => ['M', 'L']],
        'Pleteni džemper sa širokim prugama. Mešavina vune i pamuka, ne grebe.'],
    ['NIT-DZE-BEZ', 'Rebrasti džemper, bež', 'duksevi-i-dzemperi', 6490, 14641423, true, ['Veličina' => ['S', 'M', 'L']],
        'Rebrasti džemper opuštenog kroja od merino vune. Toplo i prozračno.'],
    ['NIT-FAR-SLIM', 'Farmerke Slim, tamnoplave', 'farmerke', 6990, 4546763, true, ['Veličina' => NIT_JEANS],
        'Uže farmerke od čvrstog teksasa sa malo elastina. Tamnoplava boja koja polako bledi.'],
    ['NIT-FAR-REG', 'Farmerke Regular, svetloplave', 'farmerke', 6490, 4109798, false, ['Veličina' => NIT_JEANS],
        'Ravan kroj, svetlo ispran teksas. Udobne od prvog dana.'],
    ['NIT-JAK-TEKS', 'Teksas jakna', 'jakne', 8990, 11096066, true, ['Veličina' => ['S', 'M', 'L']],
        'Klasična teksas jakna sa metalnim dugmićima i dva džepa na grudima.'],
    ['NIT-JAK-KOZ', 'Kožna jakna, crna', 'jakne', 18990, 37595197, true, ['Veličina' => ['M', 'L', 'XL']],
        'Jakna od mekane goveđe kože sa rajsferšlusom i postavom od viskoze.'],
    ['NIT-JAK-TREN', 'Trenčkot, bež', 'jakne', 14990, 5424909, false, ['Veličina' => ['S', 'M', 'L']],
        'Dvoredni trenčkot od pamučnog gabardina sa kaišem. Ne propušta vetar ni sitnu kišu.'],
    ['NIT-KAP-PLAVA', 'Pletena kapa, plava', 'aksesoari', 1490, 11630885, false, [],
        'Ručno pletena kapa od mekane vune.'],
    ['NIT-KAP-PRUG', 'Pletena kapa na pruge', 'aksesoari', 1590, 30263573, false, [],
        'Kapa sa prugama u toplim tonovima, uz džemper na pruge.'],
    ['NIT-KAC-TRUCK', 'Kačket trucker', 'aksesoari', 1990, 26956138, false, [],
        'Kačket sa mrežicom pozadi i podesivim kopčanjem.'],
    ['NIT-CAR-SET3', 'Čarape, set 3 para', 'aksesoari', 990, 9594142, true, [],
        'Tri para pamučnih čarapa u pastelnim bojama.'],
    ['NIT-TOR-PLAT', 'Platnena torba', 'aksesoari', 1290, 9869067, false, [],
        'Torba od debelog platna, nosi i do 10 kg.'],
    ['NIT-TOR-JUTA', 'Jutena torba', 'aksesoari', 1790, 12025443, true, [],
        'Čvrsta torba od jute sa pletenim ručkama.'],
];

// Varied stock, so Zalihe shows normal, low and empty items after the import.
$stock_pattern = [14, 8, 3, 0, 11, 22, 6, 2, 17, 9, 0, 25, 5, 12];
$stock_index = 0;
$next_stock = function () use ($stock_pattern, &$stock_index) {
    return $stock_pattern[$stock_index++ % count($stock_pattern)];
};

function nit_image(int $pexels_id, string $title): int
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

// Categories.
$category_ids = [];
foreach ($categories as $slug => [$name, $photo, $description]) {
    $term = get_term_by('slug', $slug, 'product_cat');
    $term_id = $term ? $term->term_id : wp_insert_term($name, 'product_cat', ['slug' => $slug, 'description' => $description])['term_id'];
    if (!get_term_meta($term_id, 'thumbnail_id', true)) {
        update_term_meta($term_id, 'thumbnail_id', nit_image($photo, $name));
    }
    $category_ids[$slug] = $term_id;
}

// Products. Each variation is a separate item (SKU) in Zalihe.
foreach ($products as [$sku, $name, $category, $price, $photo, $featured, $attributes, $short]) {
    if (wc_get_product_id_by_sku($sku)) {
        continue;
    }
    $product = $attributes ? new WC_Product_Variable() : new WC_Product_Simple();
    $product->set_name($name);
    $product->set_sku($sku);
    $product->set_short_description($short);
    $product->set_description($short . "\n\nOdržavanje: pranje na 30 °C, ne sušiti u mašini. Šiveno u Srbiji.");
    $product->set_category_ids([$category_ids[$category]]);
    $product->set_featured($featured);
    $product->set_image_id(nit_image($photo, $name));
    $product->set_reviews_allowed(false);

    if (!$attributes) {
        $product->set_regular_price((string) $price);
        $product->set_manage_stock(true);
        $product->set_stock_quantity($next_stock());
        $product->save();
        WP_CLI::log("  + $name ($sku)");
        continue;
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
        $variation->set_stock_quantity($next_stock());
        $variation->save();
    }
    WC_Product_Variable::sync($parent_id);
    WP_CLI::log("  + $name ($sku, " . count($combinations) . " variations)");
}

// Purchase price: about 45% of the sale price, rounded to tens. Set on existing products too;
// variations without their own cost use their product's.
foreach ($products as [$sku, , , $price]) {
    $product = wc_get_product(wc_get_product_id_by_sku($sku));
    $product->set_cogs_value(round($price * 0.45, -1));
    $product->save();
}

// Barcodes ("GTIN, UPC, EAN ili ISBN", WooCommerce 9.2+) on every simple product and variation that has
// none, so Zalihe can show finding an item by its barcode. EAN-13 from the 200 prefix, which GS1 keeps
// for in-store use, so no real company's barcode is borrowed: 200 + the product ID + a check digit.
function nit_ean(int $id): string
{
    $digits = '200' . str_pad((string) $id, 9, '0', STR_PAD_LEFT);
    $sum = 0;
    foreach (str_split($digits) as $i => $digit) {
        $sum += (int) $digit * ($i % 2 === 0 ? 1 : 3);
    }
    return $digits . ((10 - $sum % 10) % 10);
}

$with_barcode = 0;
foreach ($products as [$sku]) {
    $product = wc_get_product(wc_get_product_id_by_sku($sku));
    $targets = $product->is_type('variable') ? array_map('wc_get_product', $product->get_children()) : [$product];
    foreach ($targets as $target) {
        if (method_exists($target, 'set_global_unique_id') && !$target->get_global_unique_id()) {
            $target->set_global_unique_id(nit_ean($target->get_id()));
            $target->save();
            $with_barcode++;
        }
    }
}
WP_CLI::log("  barcodes added: $with_barcode");

// The test products from setup.sh and more-products.sh stay (Zalihe can still import them),
// but are hidden from the shop so it looks like one clothing brand.
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

// Pages: Serbian titles and slugs for the WooCommerce pages, no sample content.
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

function nit_page(string $slug, string $title, string $content, string $template = ''): int
{
    $page = get_page_by_path($slug);
    $data = ['post_type' => 'page', 'post_status' => 'publish', 'post_name' => $slug, 'post_title' => $title, 'post_content' => $content];
    $id = $page ? wp_update_post(['ID' => $page->ID] + $data) : wp_insert_post($data);
    if ($template) {
        update_post_meta($id, '_wp_page_template', $template);
    }
    return $id;
}

$p = fn (string $text) => "<!-- wp:paragraph -->\n<p>$text</p>\n<!-- /wp:paragraph -->\n";
$h = fn (string $text, int $level = 2) => "<!-- wp:heading {\"level\":$level} -->\n<h$level class=\"wp-block-heading\">$text</h$level>\n<!-- /wp:heading -->\n";

$story_photo = nit_image(5710046, 'Pleteni džemperi');
nit_page('o-nama', 'O nama',
    $p('Nit je mala radionica iz Beograda. Šijemo jednostavnu odeću od prirodnih materijala, u malim serijama, da bi svaki komad trajao godinama.')
    . $p('Ovo je demo prodavnica. Porudžbine se ne isporučuju, ali prolaze kroz celu kupovinu kao u pravoj prodavnici.'));

// Home page.
$hero_photo = nit_image(29906028, 'Odeća na vešalicama');
$hero_url = wp_get_attachment_image_url($hero_photo, 'full');
$story_url = wp_get_attachment_image_url($story_photo, 'large');
$shop_url = esc_url(home_url('/prodavnica/'));
$about_url = esc_url(home_url('/o-nama/'));
$category_list = implode(',', $category_ids);

$value = fn (string $title, string $text) => "<!-- wp:column -->\n<div class=\"wp-block-column\">"
    . "<!-- wp:heading {\"level\":3,\"fontSize\":\"large\"} -->\n<h3 class=\"wp-block-heading has-large-font-size\">$title</h3>\n<!-- /wp:heading -->\n"
    . $p($text) . "</div>\n<!-- /wp:column -->\n";

$home = <<<HTML
<!-- wp:cover {"url":"$hero_url","id":$hero_photo,"dimRatio":50,"overlayColor":"contrast","isUserOverlayColor":true,"minHeight":620,"contentPosition":"bottom left","align":"full","style":{"spacing":{"padding":{"top":"var:preset|spacing|60","bottom":"var:preset|spacing|60","left":"var:preset|spacing|50","right":"var:preset|spacing|50"},"margin":{"top":"0","bottom":"0"}}},"layout":{"type":"constrained"}} -->
<div class="wp-block-cover alignfull has-custom-content-position is-position-bottom-left" style="margin-top:0;margin-bottom:0;padding-top:var(--wp--preset--spacing--60);padding-right:var(--wp--preset--spacing--50);padding-bottom:var(--wp--preset--spacing--60);padding-left:var(--wp--preset--spacing--50);min-height:620px"><span aria-hidden="true" class="wp-block-cover__background has-contrast-background-color has-background-dim-50 has-background-dim"></span><img class="wp-block-cover__image-background wp-image-$hero_photo" alt="" src="$hero_url" data-object-fit="cover"/><div class="wp-block-cover__inner-container">
<!-- wp:paragraph {"textColor":"base","style":{"typography":{"textTransform":"uppercase","letterSpacing":"0.12em","fontSize":"0.8125rem"}}} -->
<p class="has-base-color has-text-color" style="font-size:0.8125rem;letter-spacing:0.12em;text-transform:uppercase">Jesen / zima 2026</p>
<!-- /wp:paragraph -->
<!-- wp:heading {"level":1,"textColor":"base","style":{"typography":{"fontSize":"clamp(2.5rem, 6vw, 4.5rem)"}}} -->
<h1 class="wp-block-heading has-base-color has-text-color" style="font-size:clamp(2.5rem, 6vw, 4.5rem)">Odeća koja traje<br>duže od sezone</h1>
<!-- /wp:heading -->
<!-- wp:paragraph {"textColor":"base","fontSize":"medium"} -->
<p class="has-base-color has-text-color has-medium-font-size">Prirodni materijali, jednostavni krojevi, šiveno u Srbiji.</p>
<!-- /wp:paragraph -->
<!-- wp:buttons -->
<div class="wp-block-buttons"><!-- wp:button {"backgroundColor":"base","textColor":"contrast"} -->
<div class="wp-block-button"><a class="wp-block-button__link has-contrast-color has-base-background-color has-text-color has-background wp-element-button" href="$shop_url">Pogledaj kolekciju</a></div>
<!-- /wp:button --></div>
<!-- /wp:buttons --></div></div>
<!-- /wp:cover -->

<!-- wp:group {"align":"full","backgroundColor":"accent-2","style":{"spacing":{"padding":{"top":"var:preset|spacing|40","bottom":"var:preset|spacing|40"},"margin":{"top":"0"}}},"layout":{"type":"constrained"}} -->
<div class="wp-block-group alignfull has-accent-2-background-color has-background" style="margin-top:0;padding-top:var(--wp--preset--spacing--40);padding-bottom:var(--wp--preset--spacing--40)">
<!-- wp:columns {"align":"wide"} -->
<div class="wp-block-columns alignwide">
{$value('Besplatna dostava', 'Za porudžbine preko 6.000 RSD, na teritoriji cele Srbije.')}{$value('Povraćaj u 14 dana', 'Ako ne odgovara, vratite bez objašnjenja.')}{$value('Plaćanje pouzećem', 'Plaćate kuriru kada preuzmete paket.')}</div>
<!-- /wp:columns --></div>
<!-- /wp:group -->

<!-- wp:group {"align":"wide","style":{"spacing":{"padding":{"top":"var:preset|spacing|60"}}},"layout":{"type":"default"}} -->
<div class="wp-block-group alignwide" style="padding-top:var(--wp--preset--spacing--60)">
{$h('Kategorije')}
<!-- wp:shortcode -->
[product_categories ids="$category_list" columns="5" orderby="include"]
<!-- /wp:shortcode --></div>
<!-- /wp:group -->

<!-- wp:group {"align":"wide","style":{"spacing":{"padding":{"top":"var:preset|spacing|50"}}},"layout":{"type":"default"}} -->
<div class="wp-block-group alignwide" style="padding-top:var(--wp--preset--spacing--50)">
{$h('Izdvajamo')}
<!-- wp:shortcode -->
[products visibility="featured" limit="8" columns="4"]
<!-- /wp:shortcode --></div>
<!-- /wp:group -->

<!-- wp:media-text {"align":"wide","mediaId":$story_photo,"mediaType":"image","verticalAlignment":"center","style":{"spacing":{"margin":{"top":"var:preset|spacing|60","bottom":"var:preset|spacing|60"}}}} -->
<div class="wp-block-media-text alignwide is-stacked-on-mobile is-vertically-aligned-center" style="margin-top:var(--wp--preset--spacing--60);margin-bottom:var(--wp--preset--spacing--60)"><figure class="wp-block-media-text__media"><img src="$story_url" alt="Pleteni džemperi" class="wp-image-$story_photo size-full"/></figure><div class="wp-block-media-text__content">
{$h('Šijemo u malim serijama')}
{$p('Svaki model pravimo u ograničenoj količini, od pamuka, vune i lana. Kada se serija rasproda, pravimo novu, pa je stanje u prodavnici uvek tačno.')}
<!-- wp:buttons -->
<div class="wp-block-buttons"><!-- wp:button -->
<div class="wp-block-button"><a class="wp-block-button__link wp-element-button" href="$about_url">O nama</a></div>
<!-- /wp:button --></div>
<!-- /wp:buttons --></div></div>
<!-- /wp:media-text -->
HTML;

$home_id = nit_page('pocetna', 'Početna', $home, 'page-no-title');
update_option('show_on_front', 'page');
update_option('page_on_front', $home_id);

// Main menu. The theme's navigation block uses the newest menu when it has none of its own.
$link = fn (string $label, string $path) => '<!-- wp:navigation-link {"label":"' . $label . '","url":"' . esc_url(home_url($path)) . '","kind":"custom"} /-->';
$menu = $link('Prodavnica', '/prodavnica/');
foreach (['majice', 'duksevi-i-dzemperi', 'jakne', 'aksesoari'] as $slug) {
    $menu .= $link($categories[$slug][0], "/kategorija/$slug/");
}
$menu .= $link('O nama', '/o-nama/');
$existing_menu = get_posts(['post_type' => 'wp_navigation', 'title' => 'Glavni meni', 'post_status' => 'publish', 'fields' => 'ids']);
$menu_data = ['post_type' => 'wp_navigation', 'post_status' => 'publish', 'post_title' => 'Glavni meni', 'post_content' => $menu];
$existing_menu ? wp_update_post(['ID' => $existing_menu[0]] + $menu_data) : wp_insert_post($menu_data);

// Shipping in Serbia and cash on delivery, so a demo order goes all the way through checkout.
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
    update_option("woocommerce_flat_rate_{$flat}_settings", ['title' => 'Kurirska služba', 'tax_status' => 'none', 'cost' => '390']);
    $free = $zone->add_shipping_method('free_shipping');
    update_option("woocommerce_free_shipping_{$free}_settings", ['title' => 'Besplatna dostava', 'requires' => 'min_amount', 'min_amount' => '6000']);
}
update_option('woocommerce_cod_settings', [
    'enabled' => 'yes',
    'title' => 'Plaćanje pouzećem',
    'description' => 'Plaćate kuriru prilikom preuzimanja paketa.',
    'instructions' => 'Plaćate kuriru prilikom preuzimanja paketa.',
    'enable_for_methods' => [],
    'enable_for_virtual' => 'yes',
]);

update_option('woocommerce_permalinks', array_merge((array) get_option('woocommerce_permalinks', []), [
    'product_base' => '/proizvod',
    'category_base' => 'kategorija',
    'tag_base' => 'oznaka',
]));
