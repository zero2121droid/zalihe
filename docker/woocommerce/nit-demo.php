<?php
/**
 * Plugin Name: Nit demo shop
 * Description: LOCAL DEMO ONLY. Look of the "Nit" demo clothing shop on Twenty Twenty-Five
 * (colors, fonts, footer) and Serbian Latin instead of Cyrillic for WordPress and WooCommerce
 * texts. Installed by docker/woocommerce/demo.sh.
 */

// Colors and fonts. The slugs are the ones Twenty Twenty-Five uses, so the theme's templates pick them up.
add_filter('wp_theme_json_data_theme', function ($theme_json) {
    return $theme_json->update_with([
        'version' => 3,
        'settings' => [
            'color' => [
                'palette' => [
                    ['slug' => 'base', 'name' => 'Base', 'color' => '#F7F4EF'],
                    ['slug' => 'contrast', 'name' => 'Contrast', 'color' => '#1F1B17'],
                    ['slug' => 'accent-1', 'name' => 'Accent 1', 'color' => '#9C4A2B'],
                    ['slug' => 'accent-2', 'name' => 'Accent 2', 'color' => '#E9DFD3'],
                    ['slug' => 'accent-3', 'name' => 'Accent 3', 'color' => '#3D4A3F'],
                    ['slug' => 'accent-4', 'name' => 'Accent 4', 'color' => '#6E665E'],
                    ['slug' => 'accent-5', 'name' => 'Accent 5', 'color' => '#FFFFFF'],
                    ['slug' => 'accent-6', 'name' => 'Accent 6', 'color' => 'color-mix(in srgb, currentColor 15%, transparent)'],
                ],
            ],
            'typography' => [
                'fontFamilies' => [
                    ['slug' => 'manrope', 'name' => 'Inter', 'fontFamily' => '"Inter", system-ui, sans-serif'],
                    ['slug' => 'fraunces', 'name' => 'Fraunces', 'fontFamily' => '"Fraunces", Georgia, serif'],
                ],
            ],
        ],
        'styles' => [
            'elements' => [
                'heading' => [
                    'typography' => ['fontFamily' => 'var:preset|font-family|fraunces', 'fontWeight' => '500', 'letterSpacing' => '-0.02em'],
                ],
                'button' => [
                    'color' => ['background' => 'var:preset|color|contrast', 'text' => 'var:preset|color|base'],
                    'border' => ['radius' => '999px'],
                    ':hover' => ['color' => ['background' => 'var:preset|color|accent-1', 'text' => 'var:preset|color|base']],
                ],
                'link' => [
                    'color' => ['text' => 'var:preset|color|contrast'],
                    ':hover' => ['color' => ['text' => 'var:preset|color|accent-1']],
                ],
            ],
            'blocks' => [
                'core/site-title' => [
                    'typography' => ['fontFamily' => 'var:preset|font-family|fraunces', 'fontSize' => '1.75rem', 'fontWeight' => '600', 'letterSpacing' => '-0.03em'],
                ],
            ],
        ],
    ]);
});

add_action('wp_enqueue_scripts', function () {
    wp_enqueue_style(
        'nit-demo-fonts',
        'https://fonts.googleapis.com/css2?family=Fraunces:opsz,wght@9..144,400..700&family=Inter:wght@400;500;600&display=swap',
        [],
        null
    );
    wp_add_inline_style('nit-demo-fonts', <<<'CSS'
        .wp-block-site-title a { text-decoration: none; }
        .nit-announcement { font-size: 0.8125rem; letter-spacing: 0.04em; }
        .woocommerce ul.products li.product a img,
        .wc-block-grid__product-image img,
        .wc-block-components-product-image img { aspect-ratio: 4 / 5; object-fit: cover; border-radius: 6px; }
        .woocommerce ul.products li.product .woocommerce-loop-product__title,
        .woocommerce ul.products li.product-category h2 { font-family: "Fraunces", Georgia, serif; font-weight: 500; font-size: 1.125rem; }
        .woocommerce ul.products li.product-category h2 mark { display: none; }
        .woocommerce ul.products li.product .price { color: var(--wp--preset--color--accent-4); }
        .woocommerce span.onsale { background: var(--wp--preset--color--accent-1); border-radius: 999px; }
        .woocommerce ul.products li.product .button { border-radius: 999px; }
        .woocommerce-Tabs-panel--description > h2:first-child { display: none; }
        CSS);
});

// Header with the menu, account and cart; footer with the shop's own links instead of the theme's sample ones.
add_action('init', function () {
    unregister_block_pattern('twentytwentyfive/header');
    register_block_pattern('twentytwentyfive/header', [
        'title' => 'Header',
        'blockTypes' => ['core/template-part/header'],
        'content' => '<!-- wp:group {"align":"full","layout":{"type":"constrained"}} --><div class="wp-block-group alignfull">'
            . '<!-- wp:group {"align":"wide","style":{"spacing":{"padding":{"top":"var:preset|spacing|30","bottom":"var:preset|spacing|30"}}},"layout":{"type":"flex","flexWrap":"nowrap","justifyContent":"space-between"}} -->'
            . '<div class="wp-block-group alignwide" style="padding-top:var(--wp--preset--spacing--30);padding-bottom:var(--wp--preset--spacing--30)">'
            . '<!-- wp:site-title {"level":0} /-->'
            . '<!-- wp:group {"style":{"spacing":{"blockGap":"var:preset|spacing|30"}},"layout":{"type":"flex","flexWrap":"nowrap","justifyContent":"right"}} --><div class="wp-block-group">'
            . '<!-- wp:navigation {"overlayBackgroundColor":"base","overlayTextColor":"contrast","metadata":{"ignoredHookedBlocks":["woocommerce/customer-account","woocommerce/mini-cart"]},"layout":{"type":"flex","justifyContent":"right","flexWrap":"wrap"}} /-->'
            . '<!-- wp:woocommerce/customer-account {"displayStyle":"icon_only","iconStyle":"line","iconClass":"wc-block-customer-account__account-icon"} /-->'
            . '<!-- wp:woocommerce/mini-cart /-->'
            . '</div><!-- /wp:group --></div><!-- /wp:group --></div><!-- /wp:group -->',
    ]);

    unregister_block_pattern('twentytwentyfive/footer');
    $home = esc_url(home_url('/'));
    $link = fn (string $path, string $label) => '<!-- wp:paragraph --><p><a href="' . esc_url(home_url($path)) . '">' . esc_html($label) . '</a></p><!-- /wp:paragraph -->';
    $column = fn (string $title, string $content) => '<!-- wp:column --><div class="wp-block-column">'
        . '<!-- wp:heading {"level":3,"fontSize":"medium"} --><h3 class="wp-block-heading has-medium-font-size">' . esc_html($title) . '</h3><!-- /wp:heading -->'
        . $content . '</div><!-- /wp:column -->';

    register_block_pattern('twentytwentyfive/footer', [
        'title' => 'Footer',
        'blockTypes' => ['core/template-part/footer'],
        'content' => '<!-- wp:group {"align":"full","backgroundColor":"contrast","textColor":"base","style":{"spacing":{"padding":{"top":"var:preset|spacing|60","bottom":"var:preset|spacing|50"}},"elements":{"link":{"color":{"text":"var:preset|color|base"}}}},"layout":{"type":"constrained"}} -->'
            . '<div class="wp-block-group alignfull has-base-color has-contrast-background-color has-text-color has-background has-link-color" style="padding-top:var(--wp--preset--spacing--60);padding-bottom:var(--wp--preset--spacing--50)">'
            . '<!-- wp:columns {"align":"wide","style":{"typography":{"fontSize":"0.9375rem"}}} --><div class="wp-block-columns alignwide" style="font-size:0.9375rem">'
            . '<!-- wp:column {"width":"40%"} --><div class="wp-block-column" style="flex-basis:40%">'
            . '<!-- wp:site-title {"level":0} /-->'
            . '<!-- wp:paragraph --><p>Jednostavna odeća od prirodnih materijala, krojena da traje. Šijemo u malim serijama u Srbiji.</p><!-- /wp:paragraph -->'
            . '</div><!-- /wp:column -->'
            . $column('Kupovina', $link('/prodavnica/', 'Prodavnica') . $link('/korpa/', 'Korpa') . $link('/moj-nalog/', 'Moj nalog'))
            . $column('Informacije', $link('/o-nama/', 'O nama') . $link('/povracaj/', 'Povraćaj i reklamacije') . $link('/politika-privatnosti/', 'Politika privatnosti'))
            . $column('Dostava', '<!-- wp:paragraph --><p>Isporuka za 1–3 radna dana. Besplatna dostava za porudžbine preko 6.000 RSD. Plaćanje pouzećem.</p><!-- /wp:paragraph -->')
            . '</div><!-- /wp:columns -->'
            . '<!-- wp:separator {"align":"wide","style":{"color":{"background":"#FFFFFF33"}}} --><hr class="wp-block-separator alignwide has-text-color has-alpha-channel-opacity has-background" style="background-color:#FFFFFF33;color:#FFFFFF33"/><!-- /wp:separator -->'
            . '<!-- wp:group {"align":"wide","layout":{"type":"default"}} --><div class="wp-block-group alignwide">'
            . '<!-- wp:paragraph {"style":{"typography":{"fontSize":"0.8125rem"}}} --><p style="font-size:0.8125rem">© Nit · Demo prodavnica, porudžbine se ne isporučuju. <a href="' . $home . '">Početna</a></p><!-- /wp:paragraph -->'
            . '</div><!-- /wp:group -->'
            . '</div><!-- /wp:group -->',
    ]);
}, 20);

// WordPress has only a Cyrillic Serbian translation (sr_RS); the shop is in Latin, like most Serbian shops.
function nit_demo_to_latin(string $text): string
{
    return strtr($text, nit_demo_to_latin_map());
}

function nit_demo_to_latin_map(): array
{
    return [
        'Љ' => 'Lj', 'Њ' => 'Nj', 'Џ' => 'Dž', 'љ' => 'lj', 'њ' => 'nj', 'џ' => 'dž',
        'А' => 'A', 'Б' => 'B', 'В' => 'V', 'Г' => 'G', 'Д' => 'D', 'Ђ' => 'Đ', 'Е' => 'E', 'Ж' => 'Ž',
        'З' => 'Z', 'И' => 'I', 'Ј' => 'J', 'К' => 'K', 'Л' => 'L', 'М' => 'M', 'Н' => 'N', 'О' => 'O',
        'П' => 'P', 'Р' => 'R', 'С' => 'S', 'Т' => 'T', 'Ћ' => 'Ć', 'У' => 'U', 'Ф' => 'F', 'Х' => 'H',
        'Ц' => 'C', 'Ч' => 'Č', 'Ш' => 'Š',
        'а' => 'a', 'б' => 'b', 'в' => 'v', 'г' => 'g', 'д' => 'd', 'ђ' => 'đ', 'е' => 'e', 'ж' => 'ž',
        'з' => 'z', 'и' => 'i', 'ј' => 'j', 'к' => 'k', 'л' => 'l', 'м' => 'm', 'н' => 'n', 'о' => 'o',
        'п' => 'p', 'р' => 'r', 'с' => 's', 'т' => 't', 'ћ' => 'ć', 'у' => 'u', 'ф' => 'f', 'х' => 'h',
        'ц' => 'c', 'ч' => 'č', 'ш' => 'š',
    ];
}

// Strings the Serbian WooCommerce translation is missing.
const NIT_DEMO_MISSING = ['SKU:' => 'Šifra:', 'Category:' => 'Kategorija:', 'Categories:' => 'Kategorije:', 'Tag:' => 'Oznaka:', 'Tags:' => 'Oznake:'];

// Latin text passes through unchanged, so this is harmless for other languages.
$nit_demo_latin = fn ($translation) => is_string($translation)
    ? nit_demo_to_latin(str_starts_with(get_locale(), 'sr') ? (NIT_DEMO_MISSING[$translation] ?? $translation) : $translation)
    : $translation;
add_filter('gettext', $nit_demo_latin, 99);
add_filter('ngettext', $nit_demo_latin, 99);
add_filter('gettext_with_context', $nit_demo_latin, 99);
add_filter('ngettext_with_context', $nit_demo_latin, 99);
// Block cart and checkout are translated in the browser, so their texts are converted there too.
add_action('wp_enqueue_scripts', function () {
    $map = wp_json_encode(nit_demo_to_latin_map());
    wp_add_inline_script('wp-i18n', <<<JS
        (function (hooks) {
            const map = $map;
            const latin = (text) => typeof text === 'string' ? text.replace(/[\\u0400-\\u04FF]/g, (c) => map[c] ?? c) : text;
            for (const filter of ['i18n.gettext', 'i18n.gettext_with_context', 'i18n.ngettext', 'i18n.ngettext_with_context']) {
                hooks.addFilter(filter, 'nit-demo/latin', latin);
            }
        })(window.wp.hooks);
        JS, 'after');
}, 1);
// The product page labels come from block attributes in WooCommerce's template, not from gettext.
add_filter('render_block', function ($html, $block) {
    if (!in_array($block['blockName'], ['woocommerce/product-sku', 'core/post-terms'], true) || !str_starts_with(get_locale(), 'sr')) {
        return $html;
    }
    return preg_replace_callback(
        '/(<span class="wp-block-post-terms__prefix">)\s*([^<]*?)\s*(<\/span>)/',
        fn ($m) => $m[1] . (NIT_DEMO_MISSING[$m[2]] ?? $m[2]) . ' ' . $m[3],
        $html
    );
}, 10, 2);
add_filter('woocommerce_currency_symbol', fn ($symbol, $currency) => $currency === 'RSD' ? 'RSD' : $symbol, 99, 2);
