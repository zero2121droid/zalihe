/** WooCommerce REST API keys as typed into the form. */
export interface Keys {
  consumerKey: string
  consumerSecret: string
}

export const emptyKeys: Keys = { consumerKey: '', consumerSecret: '' }
