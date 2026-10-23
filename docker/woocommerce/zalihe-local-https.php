<?php
/**
 * Plugin Name: Zalihe local HTTPS for REST
 * Description: LOCAL DEVELOPMENT ONLY. WooCommerce accepts API keys only over HTTPS; this makes
 * REST requests to the local test shop (http://localhost:8080) count as HTTPS. Never install
 * this on a real shop.
 */
$uri = $_SERVER['REQUEST_URI'] ?? '';
if (str_contains($uri, '/wp-json/') || str_contains($uri, 'rest_route=')) {
    $_SERVER['HTTPS'] = 'on';

    // WordPress builds media addresses with the request's scheme, so products would report their
    // images at https://localhost:8080, where nothing answers. Keep them on plain HTTP.
    // SHOP_MEDIA_HOST (e.g. 192.168.1.3:8080, set in .env) swaps localhost for the computer's
    // address on the local network, so a phone opening Zalihe can load the images too.
    $mediaHost = getenv('SHOP_MEDIA_HOST') ?: '';
    add_filter('wp_get_attachment_url', function ($url) use ($mediaHost) {
        $url = set_url_scheme($url, 'http');
        return $mediaHost === '' ? $url : str_replace('http://localhost:8080/', "http://$mediaHost/", $url);
    });
}
