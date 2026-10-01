import { Box, Center, Group, Stack } from '@mantine/core'
import { Outlet } from 'react-router'
import { LanguageSwitch } from '../components/LanguageSwitch'
import { Logo } from '../components/Logo'

/** Narrow centered column for sign-in and registration. */
export function AuthLayout() {
  return (
    <Center mih="100vh" px="md" py="xl">
      <Stack w="100%" maw={400} gap="lg">
        <Group justify="space-between">
          <Logo />
          <LanguageSwitch />
        </Group>
        <Box p="lg" bg="var(--z-surface)" bd="1px solid var(--z-line)" style={{ borderRadius: 'var(--mantine-radius-md)' }}>
          <Outlet />
        </Box>
      </Stack>
    </Center>
  )
}
