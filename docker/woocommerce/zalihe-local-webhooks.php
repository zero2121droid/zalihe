<?php
/**
 * Plugin Name: Zalihe local webhooks
 * Description: LOCAL DEVELOPMENT ONLY. Lets the test shop deliver webhooks to Zalihe running on the
 * host machine (http://host.docker.internal:5131): WordPress normally refuses private addresses and
 * ports other than 80, 443 and 8080. Webhooks are also delivered at once instead of through WP-Cron,
 * which can't run in this container (its loopback address is localhost:8080 only on the host).
 * Delivered at once, a webhook is built inside the customer's checkout request, which is a Store API
 * (wc/store) request, and WooCommerce then loads only that REST namespace: the payload, built by an
 * internal wc/v3 request, would be a "rest_no_route" error instead of the order. So wc/v3 is always
 * loaded here. Real shops deliver from a background request, where every namespace is loaded.
 * Never install this on a real shop.
 */
add_filter('http_request_host_is_external', '__return_true');
add_filter('http_allowed_safe_ports', fn($ports) => array_merge((array) $ports, [5131]));
add_filter('woocommerce_webhook_deliver_async', '__return_false');
add_filter('wc_rest_should_load_namespace', fn($load, $ns) => $load || rtrim($ns, '/') === 'wc/v3', 10, 2);
