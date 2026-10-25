<?php
/**
 * Plugin Name: Zalihe local shop connection
 * Description: LOCAL DEVELOPMENT ONLY. WooCommerce sends the keys of an approved connection (wc-auth) only
 * to an https address, so locally they go to Zalihe's https port on the host machine
 * (https://host.docker.internal:7135, `dotnet run --launch-profile https`). That port is allowed here, and
 * the .NET development certificate, which is issued for localhost and trusted by nothing in this container,
 * is accepted for that host only. Never install this on a real shop.
 */
add_filter('http_allowed_safe_ports', fn($ports) => array_merge((array) $ports, [7135]));
add_filter('http_request_args', function ($args, $url) {
    if (wp_parse_url($url, PHP_URL_HOST) === 'host.docker.internal') {
        $args['sslverify'] = false;
    }
    return $args;
}, 10, 2);
