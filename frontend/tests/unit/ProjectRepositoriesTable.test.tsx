import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ProjectRepositoriesTable } from '../../src/features/projects/components/ProjectRepositoriesTable'
import type { components } from '../../src/lib/api'

type RepositoryDto = components['schemas']['RepositoryDto']

const baseRepo: RepositoryDto = {
  id: '1',
  gitProviderConnectionId: 'conn-1',
  externalId: 'ext-1',
  name: 'my-repo',
  defaultBranch: 'main',
  webUrl: 'https://example.com',
  azureProjectName: 'MyProject',
  isTracked: true,
  lastSyncedAt: null,
  latestTag: null,
  latestTagCommitSha: null,
  latestTagSetAt: null,
  latestTagSetBy: null,
}

const pinnedRepo: RepositoryDto = {
  ...baseRepo,
  id: '2',
  name: 'pinned-repo',
  latestTag: 'v1.2.3',
  latestTagCommitSha: 'abc1234def5678',
  latestTagSetAt: '2026-05-01T10:00:00Z',
  latestTagSetBy: { id: 'user-1', email: 'admin@example.com' },
}

describe('ProjectRepositoriesTable', () => {
  it('renders badge with correct tag name when pinned', () => {
    render(<ProjectRepositoriesTable repositories={[pinnedRepo]} />)
    expect(screen.getByTestId('latest-tag-badge')).toHaveTextContent('v1.2.3')
  })

  it('tooltip shows short SHA and email', () => {
    render(<ProjectRepositoriesTable repositories={[pinnedRepo]} />)
    const tooltip = screen.getByTestId('tag-tooltip')
    expect(tooltip).toHaveTextContent('abc1234')
    expect(tooltip).toHaveTextContent('admin@example.com')
  })

  it('shows — for null tag', () => {
    render(<ProjectRepositoriesTable repositories={[baseRepo]} />)
    expect(screen.getByText('—')).toBeInTheDocument()
  })

  it('shows amber dot for null tag', () => {
    render(<ProjectRepositoriesTable repositories={[baseRepo]} />)
    expect(screen.getByTestId('amber-dot')).toBeInTheDocument()
  })

  it('falls back to Unknown user in tooltip when latestTagSetBy is null', () => {
    const repoNoUser: RepositoryDto = {
      ...pinnedRepo,
      id: '3',
      latestTagSetBy: null,
    }
    render(<ProjectRepositoriesTable repositories={[repoNoUser]} />)
    const tooltip = screen.getByTestId('tag-tooltip')
    expect(tooltip).toHaveTextContent('Unknown user')
  })
})
