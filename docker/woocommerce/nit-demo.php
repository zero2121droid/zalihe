<?php
/**
 * Plugin Name: Nit demo shop
 * Description: LOCAL DEMO ONLY. Look of the "Nit" demo clothing shop on Twenty Twenty-Five
 * (colors, fonts, header and footer). Installed by docker/woocommerce/demo.sh.
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
