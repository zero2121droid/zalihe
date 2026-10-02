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
}
