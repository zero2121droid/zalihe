<?php
/**
 * Plugin Name: Zalihe local webhooks
 * Description: LOCAL DEVELOPMENT ONLY. Lets the test shop deliver webhooks to Zalihe running on the
 * host machine (http://host.docker.internal:5131): WordPress normally refuses private addresses and
 * ports other than 80, 443 and 8080. Webhooks are also delivered at once instead of through WP-Cron,
 * which can't run in this container (its loopback address is localhost:8080 only on the host).
 * Never install this on a real shop.
 */
add_filter('http_request_host_is_external', '__return_true');
add_filter('http_allowed_safe_ports', fn($ports) => array_merge((array) $ports, [5131]));
add_filter('woocommerce_webhook_deliver_async', '__return_false');
