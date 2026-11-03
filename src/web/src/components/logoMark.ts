/**
 * The logo's box on a 16-unit grid: a lid, a gap, and a body with two eyes cut out (evenodd), so
 * the background shows through them. The loaders draw the same box, with the lid as its own shape
 * so it can open.
 */
export const MARK_LID = 'M0 1h16v4H0z'
export const MARK_BODY = 'M1 6h14v9H1z M4.5 8.5h2v3.5h-2z M9.5 8.5h2v3.5h-2z'

/**
 * The same body split up for the animated logo: the body without holes, and the eyes on their own,
 * so they can blink and look around. The logo cuts the eyes out with a mask, so they stay holes.
 */
export const MARK_BODY_SOLID = { x: 1, y: 6, width: 14, height: 9 }
export const MARK_EYES = [
  { x: 4.5, y: 8.5, width: 2, height: 3.5 },
  { x: 9.5, y: 8.5, width: 2, height: 3.5 },
]
