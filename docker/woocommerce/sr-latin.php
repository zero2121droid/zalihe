<?php
/**
 * Plugin Name: Serbian Latin for demo shops
 * Description: LOCAL DEMO ONLY. Serbian Latin instead of Cyrillic for WordPress and WooCommerce texts in the
 * demo shops ("Nit", "Zrno"). Installed by docker/woocommerce/demo.sh.
 */

// WordPress has only a Cyrillic Serbian translation (sr_RS); the shop is in Latin, like most Serbian shops.
function demo_to_latin(string $text): string
{
    return strtr($text, demo_to_latin_map());
}

function demo_to_latin_map(): array
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
const DEMO_MISSING_SR = ['SKU:' => 'Šifra:', 'Category:' => 'Kategorija:', 'Categories:' => 'Kategorije:', 'Tag:' => 'Oznaka:', 'Tags:' => 'Oznake:'];

// Latin text passes through unchanged, so this is harmless for other languages.
$demo_latin = fn ($translation) => is_string($translation)
    ? demo_to_latin(str_starts_with(get_locale(), 'sr') ? (DEMO_MISSING_SR[$translation] ?? $translation) : $translation)
    : $translation;
add_filter('gettext', $demo_latin, 99);
add_filter('ngettext', $demo_latin, 99);
add_filter('gettext_with_context', $demo_latin, 99);
add_filter('ngettext_with_context', $demo_latin, 99);
// Block cart and checkout are translated in the browser, so their texts are converted there too.
add_action('wp_enqueue_scripts', function () {
    $map = wp_json_encode(demo_to_latin_map());
    wp_add_inline_script('wp-i18n', <<<JS
        (function (hooks) {
            const map = $map;
            const latin = (text) => typeof text === 'string' ? text.replace(/[\\u0400-\\u04FF]/g, (c) => map[c] ?? c) : text;
            for (const filter of ['i18n.gettext', 'i18n.gettext_with_context', 'i18n.ngettext', 'i18n.ngettext_with_context']) {
                hooks.addFilter(filter, 'demo/latin', latin);
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
        fn ($m) => $m[1] . (DEMO_MISSING_SR[$m[2]] ?? $m[2]) . ' ' . $m[3],
        $html
    );
}, 10, 2);
add_filter('woocommerce_currency_symbol', fn ($symbol, $currency) => $currency === 'RSD' ? 'RSD' : $symbol, 99, 2);
