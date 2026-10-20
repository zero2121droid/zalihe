import classes from './Odometer.module.css'

const DIGITS = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9']

/**
 * A formatted number ("1.284", "−2", "12,5") whose digits roll to their new values, each on its own
 * wheel, like a counter. Separators and the sign stay put. Digits are matched from the right, so
 * the ones keep their wheel when the number grows a digit. Screen readers get the plain text.
 */
export function Odometer({ text }: { text: string }) {
  const chars = [...text]
  return (
    <span className={classes.odometer}>
      <span className={classes.text}>{text}</span>
      <span className={classes.wheels} aria-hidden>
        {chars.map((char, i) => {
          const fromRight = chars.length - i
          const digit = DIGITS.indexOf(char)
          return digit < 0 ? (
            <span key={`c${fromRight}`}>{char}</span>
          ) : (
            <span key={`d${fromRight}`} className={classes.digit}>
              <span className={classes.reel} style={{ transform: `translateY(${-digit * 10}%)` }}>
                {DIGITS.map((d) => (
                  <span key={d}>{d}</span>
                ))}
              </span>
            </span>
          )
        })}
      </span>
    </span>
  )
}
