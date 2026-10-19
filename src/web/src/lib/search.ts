/** Whether the typed text is in one of the fields, the way the API searches (ignoring case). */
export function matchesSearch(term: string, ...fields: Array<string | null | undefined>) {
  const needle = term.trim().toLowerCase()
  return !needle || fields.some((field) => field?.toLowerCase().includes(needle))
}
