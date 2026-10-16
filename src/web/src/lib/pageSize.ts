/** Page sizes the user can pick for a table; the API allows up to 100. */
export const PAGE_SIZES = [20, 50, 100] as const
export const DEFAULT_PAGE_SIZE = PAGE_SIZES[0]

/** A page size from the URL, falling back to the default when it is not one of the offered sizes. */
export function parsePageSize(value: string | null | undefined): number {
  const size = Number(value)
  return (PAGE_SIZES as readonly number[]).includes(size) ? size : DEFAULT_PAGE_SIZE
}
