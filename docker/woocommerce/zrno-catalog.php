<?php
// Catalog, pages, menu, shipping and payment of the "Zrno" demo coffee roastery.
// Run through demo.sh ("wp eval-file"). Safe to run again: existing products (by SKU), terms,
// images and pages are reused, and the home page, menu and settings are rewritten.
// Photos are free Pexels photos (https://www.pexels.com/license/), downloaded on the first run.

require_once __DIR__ . '/demo-lib.php';

$categories = [
    // slug => [name, Pexels photo id, description]
    'kafa-u-zrnu' => ['Kafa u zrnu', 16309237, 'Kafa iz jednog porekla i espresso mešavine.'],
    'domaca-kafa' => ['Domaća kafa', 30146008, 'Sveže mlevena kafa za džezvu.'],
    'oprema' => ['Oprema i pribor', 7657866, 'Sve za pripremu kafe kod kuće.'],
];

// The coffee test products from setup.sh become part of the shop: [sku => [category, photo, featured, short description]].
$from_setup = [
    'KF-ETI-250' => ['kafa-u-zrnu', 19052799, true, 'Cvetna i voćna, sa notama jasmina i limuna. Svetlo prženje, za filter i V60.'],
    'KF-KOL-250' => ['kafa-u-zrnu', 6280326, true, 'Uravnotežena, sa notama karamele i crvene jabuke. Srednje prženje.'],
    'KF-BRA-1000' => ['kafa-u-zrnu', 2868982, false, 'Puna i slatka, sa notama čokolade i lešnika. Odlična za espreso.'],
    'ML-RUC-01' => ['oprema', 13737044, true, 'Ručni mlin sa keramičkim žrvnjem. Mlevenje se podešava od espresa do french pressa.'],
    'FP-V60-100' => ['oprema', 7125760, false, 'Papirni filteri za V60 dripper veličine 02, 100 komada.'],
];

$products = [
    // [sku, name, category, price, Pexels photo id, featured, variation attributes, short description]
    ['ZR-KEN-250', 'Kafa Kenija AA 250 g', 'kafa-u-zrnu', 1390, 8426674, true, [],
        'Sočna i živa, sa notama crne ribizle i grejpfruta. Svetlo prženje.'],
    ['ZR-ESP-1000', 'Espresso mešavina Kućna 1 kg', 'kafa-u-zrnu', 2990, 32862007, true, ['Mlevenje' => ['Zrno', 'Espresso', 'Filter']],
        'Brazil i Gvatemala, za espreso sa gustom kremom. Tamnije prženje, mi je meljemo po vašem izboru.'],
    ['ZR-DOM-200', 'Domaća kafa 200 g', 'domaca-kafa', 390, 30146008, false, [],
        'Sveže pržena i fino mlevena kafa za džezvu. Pakovanje sa ventilom čuva aromu.'],
    ['ZR-DOM-500', 'Domaća kafa 500 g', 'domaca-kafa', 890, 296287, true, [],
        'Ista kafa kao u malom pakovanju, za porodicu ili kancelariju.'],
    ['ZR-DOM-1000', 'Domaća kafa 1 kg', 'domaca-kafa', 1690, 4085266, false, [],
        'Najveće pakovanje domaće kafe, najpovoljnije po kilogramu.'],
    ['ZR-DZE-SET', 'Set za domaću kafu', 'oprema', 4990, 29056089, true, [],
        'Bakarna džezva, dve šoljice i poslužavnik, ručno rađeni.'],
    ['ZR-MOK', 'Moka lonče', 'oprema', 3290, 17905946, false, ['Veličina' => ['3 šoljice', '6 šoljica']],
        'Aluminijumsko moka lonče za espreso na šporetu.'],
    ['ZR-FRP-600', 'French press 600 ml', 'oprema', 3990, 22129725, false, [],
        'Staklena posuda i čelični filter. Za punu, gustu kafu za četiri minuta.'],
    ['ZR-V60-02', 'V60 dripper, stakleni', 'oprema', 2890, 29498509, false, [],
        'Dripper za ručnu pripremu filter kafe, veličina 02, za jednu do četiri šolje.'],
    ['ZR-SOL-SET2', 'Šolje za espreso, set 2', 'oprema', 1590, 34153652, false, [],
        'Dve keramičke šolje debelih zidova, koje dugo čuvaju toplotu.'],
    ['ZR-MLI-RETRO', 'Ručni mlin Retro', 'oprema', 7990, 19128433, true, [],
        'Drveni mlin sa fiokom i čeličnim žrvnjem, kao kod bake.'],
];

$category_ids = demo_categories($categories);

foreach ($from_setup as $sku => [$category, $photo, $featured, $short]) {
    $product = wc_get_product(wc_get_product_id_by_sku($sku));
    if (!$product || $product->get_image_id()) {
        continue;
    }
    $product->set_category_ids([$category_ids[$category]]);
    $product->set_image_id(demo_image($photo, $product->get_name()));
    $product->set_featured($featured);
    $product->set_short_description($short);
    $product->set_description($short);
    $product->set_reviews_allowed(false);
    $product->save();
    WP_CLI::log('  ~ ' . $product->get_name() . " ($sku)");
}

foreach ($products as [$sku, $name, $category, $price, $photo, $featured, $attributes, $short]) {
    demo_product($sku, $name, $category_ids[$category], $price, $photo, $featured, $attributes, $short,
        $short . "\n\nPržimo utorkom i petkom i šaljemo isti dan. Na svakom pakovanju je datum prženja.");
}

$prices = array_column($products, 3, 0);
foreach (array_keys($from_setup) as $sku) {
    $prices[$sku] = (int) wc_get_product(wc_get_product_id_by_sku($sku))->get_regular_price();
}
demo_set_costs($prices);
demo_add_barcodes(array_keys($prices));
// The shop shows only coffee and coffee gear.
demo_hide_other_products($category_ids);
demo_serbian_pages();

$p = 'demo_p';
$h = 'demo_h';

demo_page('o-nama', 'O nama',
    $p('Zrno je mala pržionica iz Novog Sada. Kafu kupujemo od malih farmi, pržimo je u malim serijama i šaljemo dok je sveža.')
    . $p('Ovo je demo prodavnica. Porudžbine se ne isporučuju, ali prolaze kroz celu kupovinu kao u pravoj prodavnici.'));

// Home page.
$hero_photo = demo_image(585754, 'Šolja crne kafe i zrna');
$story_photo = demo_image(6205579, 'Pržionica');
$hero_url = wp_get_attachment_image_url($hero_photo, 'full');
$story_url = wp_get_attachment_image_url($story_photo, 'large');
$beans_url = esc_url(home_url('/kategorija/kafa-u-zrnu/'));
$about_url = esc_url(home_url('/o-nama/'));
$category_list = implode(',', $category_ids);

$value = fn (string $title, string $text) => "<!-- wp:column -->\n<div class=\"wp-block-column\">"
    . "<!-- wp:heading {\"level\":3,\"fontSize\":\"large\"} -->\n<h3 class=\"wp-block-heading has-large-font-size\">$title</h3>\n<!-- /wp:heading -->\n"
    . $p($text) . "</div>\n<!-- /wp:column -->\n";

$home = <<<HTML
<!-- wp:cover {"url":"$hero_url","id":$hero_photo,"dimRatio":40,"overlayColor":"contrast","isUserOverlayColor":true,"minHeight":560,"contentPosition":"center left","align":"full","style":{"spacing":{"padding":{"top":"var:preset|spacing|60","bottom":"var:preset|spacing|60","left":"var:preset|spacing|50","right":"var:preset|spacing|50"},"margin":{"top":"0","bottom":"0"}}},"layout":{"type":"constrained"}} -->
<div class="wp-block-cover alignfull has-custom-content-position is-position-center-left" style="margin-top:0;margin-bottom:0;padding-top:var(--wp--preset--spacing--60);padding-right:var(--wp--preset--spacing--50);padding-bottom:var(--wp--preset--spacing--60);padding-left:var(--wp--preset--spacing--50);min-height:560px"><span aria-hidden="true" class="wp-block-cover__background has-contrast-background-color has-background-dim-40 has-background-dim"></span><img class="wp-block-cover__image-background wp-image-$hero_photo" alt="" src="$hero_url" data-object-fit="cover"/><div class="wp-block-cover__inner-container">
<!-- wp:paragraph {"textColor":"accent-2","style":{"typography":{"textTransform":"uppercase","letterSpacing":"0.12em","fontSize":"0.8125rem","fontWeight":"600"}}} -->
<p class="has-accent-2-color has-text-color" style="font-size:0.8125rem;font-weight:600;letter-spacing:0.12em;text-transform:uppercase">Novo u ponudi: Kenija AA</p>
<!-- /wp:paragraph -->
<!-- wp:heading {"level":1,"textColor":"base","style":{"typography":{"fontSize":"clamp(2.75rem, 6vw, 4.75rem)","lineHeight":"1.05"}}} -->
<h1 class="wp-block-heading has-base-color has-text-color" style="font-size:clamp(2.75rem, 6vw, 4.75rem);line-height:1.05">Kafa pržena<br>ove nedelje</h1>
<!-- /wp:heading -->
<!-- wp:paragraph {"textColor":"base","fontSize":"medium"} -->
<p class="has-base-color has-text-color has-medium-font-size">Specijalna kafa sa malih farmi, pržena u Novom Sadu i poslata isti dan.</p>
<!-- /wp:paragraph -->
<!-- wp:buttons -->
<div class="wp-block-buttons"><!-- wp:button -->
<div class="wp-block-button"><a class="wp-block-button__link wp-element-button" href="$beans_url">Izaberi kafu</a></div>
<!-- /wp:button --></div>
<!-- /wp:buttons --></div></div>
<!-- /wp:cover -->

<!-- wp:group {"align":"full","backgroundColor":"accent-2","style":{"spacing":{"padding":{"top":"var:preset|spacing|40","bottom":"var:preset|spacing|40"},"margin":{"top":"0"}}},"layout":{"type":"constrained"}} -->
<div class="wp-block-group alignfull has-accent-2-background-color has-background" style="margin-top:0;padding-top:var(--wp--preset--spacing--40);padding-bottom:var(--wp--preset--spacing--40)">
<!-- wp:columns {"align":"wide"} -->
<div class="wp-block-columns alignwide">
{$value('Sveže prženo', 'Pržimo utorkom i petkom. Datum prženja je na svakom pakovanju.')}{$value('Besplatna dostava', 'Za porudžbine preko 3.000 RSD, na teritoriji cele Srbije.')}{$value('Plaćanje pouzećem', 'Plaćate kuriru kada preuzmete paket.')}</div>
<!-- /wp:columns --></div>
<!-- /wp:group -->

<!-- wp:group {"align":"wide","style":{"spacing":{"padding":{"top":"var:preset|spacing|60"}}},"layout":{"type":"default"}} -->
<div class="wp-block-group alignwide" style="padding-top:var(--wp--preset--spacing--60)">
{$h('Izdvajamo')}
<!-- wp:shortcode -->
[products visibility="featured" limit="8" columns="4"]
<!-- /wp:shortcode --></div>
<!-- /wp:group -->

<!-- wp:group {"align":"wide","style":{"spacing":{"padding":{"top":"var:preset|spacing|50"}}},"layout":{"type":"default"}} -->
<div class="wp-block-group alignwide" style="padding-top:var(--wp--preset--spacing--50)">
{$h('Kategorije')}
<!-- wp:shortcode -->
[product_categories ids="$category_list" columns="3" orderby="include"]
<!-- /wp:shortcode --></div>
<!-- /wp:group -->

<!-- wp:media-text {"align":"wide","mediaPosition":"right","mediaId":$story_photo,"mediaType":"image","verticalAlignment":"center","style":{"spacing":{"margin":{"top":"var:preset|spacing|60","bottom":"var:preset|spacing|60"}}}} -->
<div class="wp-block-media-text alignwide has-media-on-the-right is-stacked-on-mobile is-vertically-aligned-center" style="margin-top:var(--wp--preset--spacing--60);margin-bottom:var(--wp--preset--spacing--60)"><div class="wp-block-media-text__content">
{$h('Pržimo u malim serijama')}
{$p('Svaku kafu pržimo posebno, po meri njenog porekla. Serija je mala, pa kafa ne stoji na polici: od prženja do vaše šolje prođe najviše nekoliko dana.')}
<!-- wp:buttons -->
<div class="wp-block-buttons"><!-- wp:button -->
<div class="wp-block-button"><a class="wp-block-button__link wp-element-button" href="$about_url">O nama</a></div>
<!-- /wp:button --></div>
<!-- /wp:buttons --></div><figure class="wp-block-media-text__media"><img src="$story_url" alt="Pržionica" class="wp-image-$story_photo size-full"/></figure></div>
<!-- /wp:media-text -->
HTML;

demo_home_page($home);

$menu = ['Prodavnica' => '/prodavnica/'];
foreach ($categories as $slug => [$name]) {
    $menu[$name] = "/kategorija/$slug/";
}
demo_menu($menu + ['O nama' => '/o-nama/']);

demo_shipping_and_cod(350, 3000);
