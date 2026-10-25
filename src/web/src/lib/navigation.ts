/** Leaves the app for another site, e.g. the shop's approval page. On its own so tests can replace it. */
export function leaveTo(url: string) {
  window.location.assign(url)
}
