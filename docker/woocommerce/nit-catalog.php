<?php
// Catalog, pages, menu, shipping and payment of the "Nit" demo clothing shop.
// Run through demo.sh ("wp eval-file"). Safe to run again: existing products (by SKU), terms,
// images and pages are reused, and the home page, menu and settings are rewritten.
// Photos are free Pexels photos (https://www.pexels.com/license/), downloaded on the first run.

require_once __DIR__ . '/demo-lib.php';

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

$category_ids = demo_categories($categories);
foreach ($products as [$sku, $name, $category, $price, $photo, $featured, $attributes, $short]) {
    demo_product($sku, $name, $category_ids[$category], $price, $photo, $featured, $attributes, $short,
        $short . "\n\nOdržavanje: pranje na 30 °C, ne sušiti u mašini. Šiveno u Srbiji.");
}
demo_set_costs(array_column($products, 3, 0));
demo_add_barcodes(array_column($products, 0));
// The shop looks like one clothing brand.
demo_hide_other_products($category_ids);
demo_serbian_pages();

$p = 'demo_p';
$h = 'demo_h';

$story_photo = demo_image(5710046, 'Pleteni džemperi');
demo_page('o-nama', 'O nama',
    $p('Nit je mala radionica iz Beograda. Šijemo jednostavnu odeću od prirodnih materijala, u malim serijama, da bi svaki komad trajao godinama.')
    . $p('Ovo je demo prodavnica. Porudžbine se ne isporučuju, ali prolaze kroz celu kupovinu kao u pravoj prodavnici.'));

// Home page.
$hero_photo = demo_image(29906028, 'Odeća na vešalicama');
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

demo_home_page($home);

$menu = ['Prodavnica' => '/prodavnica/'];
foreach (['majice', 'duksevi-i-dzemperi', 'jakne', 'aksesoari'] as $slug) {
    $menu[$categories[$slug][0]] = "/kategorija/$slug/";
}
demo_menu($menu + ['O nama' => '/o-nama/']);

demo_shipping_and_cod(390, 6000);
