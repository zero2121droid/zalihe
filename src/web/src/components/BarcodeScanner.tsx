import { Button, Loader, Text } from '@mantine/core'
import { useEffect, useEffectEvent, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { createDetector } from '../lib/barcodeDetector'
import classes from './BarcodeScanner.module.css'

/** How often the picture is read; often enough to feel instant, rare enough for a phone. */
const READ_EVERY_MS = 250

type Problem = 'denied' | 'missing' | 'failed'

/**
 * The back camera with a frame to aim at a barcode. The first code read is handed over and the
 * camera turns off; it also turns off when the scanner closes. Nothing is recorded or sent anywhere.
 */
export function BarcodeScanner({ onDetected, onCancel }: { onDetected: (code: string) => void; onCancel: () => void }) {
  const { t } = useTranslation()
  const video = useRef<HTMLVideoElement>(null)
  const [ready, setReady] = useState(false)
  const [problem, setProblem] = useState<Problem | null>(null)
  const detected = useEffectEvent(onDetected)

  useEffect(() => {
    let stream: MediaStream | null = null
    let timer = 0
    let stopped = false

    async function start() {
      try {
        const [media, detector] = await Promise.all([
          navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false }),
          createDetector(),
        ])
        stream = media
        if (stopped || !video.current) return stop()
        video.current.srcObject = media
        await video.current.play()
        setReady(true)
        timer = window.setInterval(async () => {
          if (!video.current || video.current.readyState < 2) return
          const [code] = await detector.detect(video.current).catch(() => [])
          if (code?.rawValue && !stopped) {
            stop()
            detected(code.rawValue.trim())
          }
        }, READ_EVERY_MS)
      } catch (error) {
        const name = (error as { name?: string }).name
        setProblem(name === 'NotAllowedError' ? 'denied' : name === 'NotFoundError' ? 'missing' : 'failed')
      }
    }

    function stop() {
      stopped = true
      window.clearInterval(timer)
      stream?.getTracks().forEach((track) => track.stop())
    }

    void start()
    return stop
  }, [])

  return (
    <div>
      {problem ? (
        <Text p="md" fz="sm" role="alert">
          {t(`common.quickSearch.camera.${problem}`)}
        </Text>
      ) : (
        <div className={classes.view}>
          <video ref={video} className={classes.video} muted playsInline aria-label={t('common.quickSearch.camera.view')} />
          {ready ? <div className={classes.frame} aria-hidden /> : <Loader pos="absolute" top="50%" left="50%" size="sm" />}
        </div>
      )}
      <div className={classes.bar}>
        <Text fz="sm" c="dimmed" role="status">
          {problem ? '' : t('common.quickSearch.camera.hint')}
        </Text>
        <Button variant="default" onClick={onCancel}>
          {t('common.quickSearch.camera.cancel')}
        </Button>
      </div>
    </div>
  )
}
