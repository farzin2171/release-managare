/**
 * Hand-crafted types matching the API contract.
 * Run `npm run codegen` (with backend on :5000) to regenerate from OpenAPI spec.
 */

export interface paths {
  "/api/v1/auth/setup": {
    post: {
      requestBody: { content: { "application/json": components["schemas"]["SetupDto"] } }
      responses: {
        201: { content: { "application/json": components["schemas"]["UserDto"] } }
        410: { content: never }
      }
    }
  }
  "/api/v1/auth/login": {
    post: {
      requestBody: { content: { "application/json": components["schemas"]["LoginDto"] } }
      responses: {
        200: { content: { "application/json": components["schemas"]["TokenResponseDto"] } }
        401: { content: never }
      }
    }
  }
  "/api/v1/auth/refresh": {
    post: {
      requestBody: { content: { "application/json": { refreshToken: string } } }
      responses: {
        200: { content: { "application/json": components["schemas"]["TokenResponseDto"] } }
      }
    }
  }
  "/api/v1/users": {
    get: {
      responses: { 200: { content: { "application/json": components["schemas"]["UserDto"][] } } }
    }
    post: {
      requestBody: { content: { "application/json": components["schemas"]["CreateUserDto"] } }
      responses: { 201: { content: { "application/json": components["schemas"]["UserDto"] } } }
    }
  }
  "/api/v1/users/{id}": {
    put: {
      parameters: { path: { id: string } }
      requestBody: { content: { "application/json": components["schemas"]["UpdateUserDto"] } }
      responses: { 200: { content: { "application/json": components["schemas"]["UserDto"] } } }
    }
    delete: {
      parameters: { path: { id: string } }
      responses: { 204: { content: never } }
    }
  }
  "/api/v1/integrations/git/test": {
    post: {
      requestBody: { content: { "application/json": components["schemas"]["TestGitConnectionDto"] } }
      responses: {
        200: { content: { "application/json": components["schemas"]["TestConnectionResultDto"] } }
      }
    }
  }
  "/api/v1/integrations/git": {
    get: {
      responses: {
        200: { content: { "application/json": components["schemas"]["GitConnectionDto"][] } }
      }
    }
    post: {
      requestBody: { content: { "application/json": components["schemas"]["CreateGitConnectionDto"] } }
      responses: {
        201: { content: { "application/json": components["schemas"]["GitConnectionDto"] } }
      }
    }
  }
  "/api/v1/integrations/git/{id}": {
    put: {
      parameters: { path: { id: string } }
      requestBody: { content: { "application/json": components["schemas"]["UpdateGitConnectionDto"] } }
      responses: {
        200: { content: { "application/json": components["schemas"]["GitConnectionDto"] } }
      }
    }
  }
  "/api/v1/integrations/git/{id}/sync": {
    post: {
      parameters: { path: { id: string } }
      responses: {
        202: { content: { "application/json": { message: string; connectionId: string } } }
      }
    }
  }
  "/api/v1/integrations/confluence/test": {
    post: {
      requestBody: {
        content: { "application/json": components["schemas"]["TestConfluenceConnectionDto"] }
      }
      responses: {
        200: { content: { "application/json": components["schemas"]["TestConnectionResultDto"] } }
      }
    }
  }
  "/api/v1/integrations/confluence": {
    get: {
      responses: {
        200: {
          content: { "application/json": components["schemas"]["ConfluenceConnectionDto"] | null }
        }
      }
    }
    put: {
      requestBody: {
        content: { "application/json": components["schemas"]["UpsertConfluenceConnectionDto"] }
      }
      responses: {
        200: { content: { "application/json": components["schemas"]["ConfluenceConnectionDto"] } }
      }
    }
  }
  "/api/v1/integrations/jira/test": {
    post: {
      requestBody: {
        content: { "application/json": components["schemas"]["TestJiraConnectionDto"] }
      }
      responses: {
        200: { content: { "application/json": components["schemas"]["TestConnectionResultDto"] } }
      }
    }
  }
  "/api/v1/integrations/jira": {
    get: {
      responses: {
        200: {
          content: { "application/json": components["schemas"]["JiraConnectionResponseDto"] | null }
        }
      }
    }
    put: {
      requestBody: {
        content: { "application/json": components["schemas"]["UpsertJiraConnectionDto"] }
      }
      responses: {
        200: { content: { "application/json": components["schemas"]["JiraConnectionResponseDto"] } }
      }
    }
  }
  "/api/v1/integrations/jira/projects": {
    get: {
      responses: {
        200: { content: { "application/json": components["schemas"]["JiraProjectDto"][] } }
      }
    }
  }
  "/api/v1/repositories": {
    get: {
      parameters: {
        query?: { connectionId?: string; isTracked?: boolean; search?: string }
      }
      responses: {
        200: { content: { "application/json": components["schemas"]["RepositoryDto"][] } }
      }
    }
  }
  "/api/v1/repositories/{id}": {
    patch: {
      parameters: { path: { id: string } }
      requestBody: { content: { "application/json": { isTracked: boolean } } }
      responses: {
        200: { content: { "application/json": components["schemas"]["RepositoryDto"] } }
      }
    }
  }
  "/api/v1/repositories/{id}/tags": {
    get: {
      parameters: { path: { id: string } }
      responses: {
        200: { content: { "application/json": { tags: components["schemas"]["RepositoryTagDto"][] } } }
        404: { content: never }
        422: { content: never }
      }
    }
  }
  "/api/v1/repositories/{id}/latest-tag": {
    put: {
      parameters: { path: { id: string } }
      requestBody: { content: { "application/json": { tagName: string } } }
      responses: {
        200: { content: { "application/json": components["schemas"]["RepositoryDto"] } }
        404: { content: never }
        422: { content: never }
      }
    }
    delete: {
      parameters: { path: { id: string } }
      responses: {
        204: { content: never }
        404: { content: never }
      }
    }
  }
  "/api/v1/projects": {
    get: {
      responses: {
        200: { content: { "application/json": components["schemas"]["ProjectDto"][] } }
      }
    }
    post: {
      requestBody: { content: { "application/json": components["schemas"]["CreateProjectDto"] } }
      responses: {
        201: { content: { "application/json": components["schemas"]["ProjectDto"] } }
      }
    }
  }
  "/api/v1/projects/{id}": {
    get: {
      parameters: { path: { id: string } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ProjectDetailDto"] } }
      }
    }
    put: {
      parameters: { path: { id: string } }
      requestBody: { content: { "application/json": components["schemas"]["UpdateProjectDto"] } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ProjectDetailDto"] } }
      }
    }
    delete: {
      parameters: { path: { id: string } }
      responses: { 204: { content: never } }
    }
  }
  "/api/v1/projects/{id}/repositories/{repoId}": {
    post: {
      parameters: { path: { id: string; repoId: string } }
      requestBody: { content: { "application/json": { isPrimary: boolean } } }
      responses: { 201: { content: never } }
    }
    delete: {
      parameters: { path: { id: string; repoId: string } }
      responses: { 204: { content: never } }
    }
  }
  "/api/v1/projects/{id}/jira": {
    put: {
      parameters: { path: { id: string } }
      requestBody: {
        content: { "application/json": components["schemas"]["ConfigureProjectJiraDto"] }
      }
      responses: {
        200: { content: { "application/json": components["schemas"]["ProjectDetailDto"] } }
      }
    }
  }
  "/api/v1/repositories/{id}/jira-coverage": {
    get: {
      parameters: {
        path: { id: string }
        query?: { refresh?: boolean }
      }
      responses: {
        200: { content: { "application/json": components["schemas"]["RepoJiraComparisonDto"] } }
        404: { content: never }
      }
    }
  }
  "/api/v1/repositories/{id}/jira-coverage/add-ticket": {
    post: {
      parameters: { path: { id: string } }
      requestBody: { content: { "application/json": { ticketKey: string } } }
      responses: {
        200: { content: { "application/json": components["schemas"]["AddToFixVersionResultDto"] } }
        404: { content: never }
        409: { content: never }
        422: { content: never }
      }
    }
  }
  "/api/v1/projects/{id}/jira-coverage": {
    get: {
      parameters: {
        path: { id: string }
        query?: { refresh?: boolean }
      }
      responses: {
        200: { content: { "application/json": components["schemas"]["ProjectJiraCoverageDto"] } }
        404: { content: never }
      }
    }
  }
  "/api/v1/repositories/{id}/changes": {
    get: {
      parameters: {
        path: { id: string }
        query?: {
          groupBy?: "ticket" | "commit" | "contributor"
          type?: string
          contributor?: string
          search?: string
        }
      }
      responses: {
        200: { content: { "application/json": components["schemas"]["RepositoryChangesDto"] } }
      }
    }
  }
  "/api/v1/projects/{id}/changes": {
    get: {
      parameters: { path: { id: string } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ProjectChangesDto"] } }
      }
    }
  }
  "/api/v1/projects/{id}/releases": {
    get: {
      parameters: {
        path: { id: string }
        query?: { status?: string; search?: string; sort?: string; order?: string }
      }
      responses: {
        200: { content: { "application/json": components["schemas"]["ReleaseSummaryDto"][] } }
      }
    }
    post: {
      parameters: { path: { id: string } }
      requestBody: { content: { "application/json": components["schemas"]["CreateReleaseRequest"] } }
      responses: {
        201: { content: { "application/json": components["schemas"]["ReleaseDetailDto"] } }
      }
    }
  }
  "/api/v1/projects/{id}/releases/preview": {
    post: {
      parameters: { path: { id: string } }
      requestBody: { content: { "application/json": { repositoryIds: string[] } } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ReleasePreviewDto"] } }
      }
    }
  }
  "/api/v1/releases/{id}": {
    get: {
      parameters: { path: { id: string } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ReleaseDetailDto"] } }
      }
    }
    put: {
      parameters: { path: { id: string } }
      requestBody: { content: { "application/json": components["schemas"]["UpdateReleaseNotesDto"] } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ReleaseDetailDto"] } }
      }
    }
  }
  "/api/v1/releases/{id}/publish": {
    post: {
      parameters: { path: { id: string } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ReleaseDetailDto"] } }
      }
    }
  }
  "/api/v1/releases/{id}/reconcile": {
    post: {
      parameters: { path: { id: string } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ReconciliationResultDto"] } }
      }
    }
  }
  "/api/v1/releases/{id}/reconciliation": {
    get: {
      parameters: { path: { id: string } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ReconciliationResultDto"] | null } }
      }
    }
  }
  "/api/v1/releases/{id}/reconciliation/jira-tickets": {
    post: {
      parameters: { path: { id: string } }
      requestBody: { content: { "application/json": { ticketKeys: string[] } } }
      responses: { 204: { content: never } }
    }
  }
  "/api/v1/templates": {
    get: {
      responses: {
        200: { content: { "application/json": components["schemas"]["TemplateDto"][] } }
      }
    }
    post: {
      requestBody: { content: { "application/json": components["schemas"]["CreateTemplateDto"] } }
      responses: {
        201: { content: { "application/json": components["schemas"]["TemplateDto"] } }
      }
    }
  }
  "/api/v1/templates/{id}": {
    put: {
      parameters: { path: { id: string } }
      requestBody: { content: { "application/json": components["schemas"]["UpdateTemplateDto"] } }
      responses: {
        200: { content: { "application/json": components["schemas"]["TemplateDto"] } }
      }
    }
    delete: {
      parameters: { path: { id: string } }
      responses: { 204: { content: never } }
    }
  }

  "/api/v1/projects/{projectId}/template-bindings": {
    get: {
      parameters: { path: { projectId: string } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ProjectTemplateBindingDto"][] } }
      }
    }
    post: {
      parameters: { path: { projectId: string } }
      requestBody: { content: { "application/json": components["schemas"]["CreateBindingRequest"] } }
      responses: {
        201: { content: { "application/json": components["schemas"]["ProjectTemplateBindingDto"] } }
      }
    }
  }
  "/api/v1/projects/{projectId}/template-bindings/{bindingId}": {
    put: {
      parameters: { path: { projectId: string; bindingId: string } }
      requestBody: { content: { "application/json": components["schemas"]["UpdateBindingRequest"] } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ProjectTemplateBindingDto"] } }
      }
    }
    delete: {
      parameters: { path: { projectId: string; bindingId: string } }
      responses: { 204: { content: never } }
    }
  }
  "/api/v1/projects/{projectId}/template-bindings/reorder": {
    post: {
      parameters: { path: { projectId: string } }
      requestBody: { content: { "application/json": { orderedIds: string[] } } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ProjectTemplateBindingDto"][] } }
      }
    }
  }

  "/api/v1/projects/{projectId}/custom-variables": {
    get: {
      parameters: { path: { projectId: string } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ProjectCustomVariableDto"][] } }
      }
    }
  }
  "/api/v1/projects/{projectId}/custom-variables/{key}": {
    put: {
      parameters: { path: { projectId: string; key: string } }
      requestBody: { content: { "application/json": { value: string } } }
      responses: {
        200: { content: { "application/json": components["schemas"]["ProjectCustomVariableDto"] } }
      }
    }
    delete: {
      parameters: { path: { projectId: string; key: string } }
      responses: { 204: { content: never } }
    }
  }

  "/api/v1/releases/{id}/prepare-pages": {
    post: {
      parameters: { path: { id: string } }
      requestBody: { content: { "application/json": components["schemas"]["PreparePageRequest"] } }
      responses: {
        200: { content: { "application/json": components["schemas"]["PreparedReleaseDto"] } }
      }
    }
  }
}

export type webhooks = Record<string, never>

export interface components {
  schemas: {
    LoginDto: { email: string; password: string }
    SetupDto: { email: string; password: string }
    TokenResponseDto: { accessToken: string; refreshToken: string; expiresAt: string }
    UserDto: {
      id: string
      email: string
      role: "Admin" | "Viewer"
      isActive: boolean
      createdAt: string
      lastLoginAt: string | null
    }
    CreateUserDto: { email: string; password: string; role: "Admin" | "Viewer" }
    UpdateUserDto: { role?: "Admin" | "Viewer"; isActive?: boolean; password?: string }

    GitConnectionDto: {
      id: string
      name: string
      providerType: "AzureDevOps"
      organizationUrl: string
      isActive: boolean
      lastSyncedAt: string | null
      lastTestStatus: "Success" | "Failed" | "Untested" | null
    }
    CreateGitConnectionDto: {
      name: string
      providerType: "AzureDevOps"
      organizationUrl: string
      pat: string
    }
    UpdateGitConnectionDto: {
      name: string
      organizationUrl: string
      pat?: string
    }
    TestGitConnectionDto: {
      providerType: "AzureDevOps"
      organizationUrl: string
      pat: string
    }
    TestConnectionResultDto: { success: boolean; message: string }

    ConfluenceConnectionDto: {
      id: string
      baseUrl: string
      email: string
      isActive: boolean
      lastTestStatus: "Success" | "Failed" | "Untested" | null
    }
    UpsertConfluenceConnectionDto: { baseUrl: string; email: string; apiToken: string }
    TestConfluenceConnectionDto: { baseUrl: string; email: string; apiToken: string }

    JiraConnectionResponseDto: {
      id: string
      baseUrl: string
      email: string
      isActive: boolean
      lastTestStatus: "Success" | "Failed" | "Untested" | null
    }
    UpsertJiraConnectionDto: { baseUrl: string; email: string; apiToken: string }
    TestJiraConnectionDto: { baseUrl: string; email: string; apiToken: string }
    JiraProjectDto: { key: string; name: string; projectType: string }

    RepositoryDto: {
      id: string
      gitProviderConnectionId: string
      externalId: string
      name: string
      defaultBranch: string
      webUrl: string
      azureProjectName: string
      isTracked: boolean
      serviceOwner: string | null
      lastSyncedAt: string | null
      latestTag: string | null
      latestTagCommitSha: string | null
      latestTagSetAt: string | null
      latestTagSetBy: components["schemas"]["UserSummaryDto"] | null
    }
    UserSummaryDto: {
      id: string
      email: string
    }
    RepositoryTagDto: {
      name: string
      commitSha: string
      commitDate: string | null
      authorName: string | null
    }

    ProjectDto: {
      id: string
      name: string
      description: string | null
      color: string
      createdAt: string
      releaseNoteTemplateId: string | null
    }
    ProjectDetailDto: {
      id: string
      name: string
      description: string | null
      color: string
      createdAt: string
      releaseNoteTemplateId: string | null
      jiraConnectionId: string | null
      jiraProjectKeys: string[]
      fixVersionPattern: string | null
      autoCreateFixVersion: boolean
      matchSubtasksToParents: boolean
      confluenceSpaceKey: string | null
      confluenceParentPageId: string | null
      repositories: components['schemas']['ProjectRepositoryDto'][]
    }
    CreateProjectDto: { name: string; description?: string; color: string }
    UpdateProjectDto: {
      name: string
      description?: string
      color: string
      releaseNoteTemplateId?: string | null
    }
    ConfigureProjectJiraDto: {
      jiraConnectionId: string
      jiraProjectKeys: string[]
      fixVersionPattern?: string | null
      autoCreateFixVersion: boolean
      matchSubtasksToParents: boolean
    }
    ProjectRepositoryDto: {
      id: string
      repositoryId: string
      name: string
      defaultBranch: string
      isPrimary: boolean
    }

    CommitDto: {
      sha: string
      shortSha: string
      message: string
      author: string
      committedAt: string
    }
    ChangeSummaryDto: {
      commitCount: number
      ticketCount: number
      breakingCount: number
      contributorCount: number
    }
    TicketGroupDto: {
      key: string
      title: string
      type: string
      isBreaking: boolean
      commitCount: number
      contributorCount: number
      commits: components["schemas"]["CommitDto"][]
    }
    RepositoryChangesDto: {
      repositoryId: string
      repositoryName: string
      fromTag: string | null
      toTag: string
      summary: components["schemas"]["ChangeSummaryDto"]
      groups: components["schemas"]["TicketGroupDto"][]
      unscoped: components["schemas"]["CommitDto"][]
    }
    ProjectChangesDto: {
      projectId: string
      projectName: string
      summary: components["schemas"]["ChangeSummaryDto"]
      groups: components["schemas"]["TicketGroupDto"][]
      unscoped: components["schemas"]["CommitDto"][]
      repositories: components["schemas"]["RepositoryChangesDto"][]
    }

    ReleaseRepositoryTagDto: {
      repositoryId: string
      repositoryName: string
      fromTag: string | null
      toTag: string
      commitCount: number
    }
    ReleaseSummaryDto: {
      id: string
      name: string
      version: string
      status: "Draft" | "Published" | "Archived"
      createdAt: string
      publishedAt: string | null
      repoCount: number
    }
    ReleaseRepositoryDto: {
      id: string
      repositoryId: string
      repositoryName: string
      previousVersion: string
      nextVersion: string
      bumpType: string
      fromCommitSha: string
      toCommitSha: string
      commitCount: number
      ticketCount: number
      isLegacy: boolean
    }
    ReleaseDetailDto: {
      id: string
      projectId: string
      version: string
      status: "Draft" | "Published"
      templateId: string | null
      generatedNotesMarkdown: string
      editedNotesMarkdown: string | null
      confluencePageId: string | null
      confluencePageUrl: string | null
      publishedAt: string | null
      createdAt: string
      repositoryTags: components["schemas"]["ReleaseRepositoryTagDto"][]
      releaseRepositories?: components["schemas"]["ReleaseRepositoryDto"][]
    }
    CreateReleaseDto: {
      version: string
      templateId: string | null
      repositoryTags: { repositoryId: string; fromTag: string | null; toTag: string }[]
    }
    CreateReleaseRequest: {
      name: string
      repositories: { repositoryId: string; nextVersion: string; bumpType: string }[]
    }
    ReleasePreviewDto: {
      repositories: components["schemas"]["ReleasePreviewRepoDto"][]
      derivedReleaseVersion: string
      derivedFromRepositoryId: string
    }
    ReleasePreviewRepoDto: {
      repositoryId: string
      name: string
      isPrimary: boolean
      hasChanges: boolean
      previousVersion: string
      suggestedNextVersion: string
      bumpType: string
      commitCount: number
      ticketCount: number
    }
    UpdateReleaseNotesDto: { editedNotesMarkdown: string }

    TemplateDto: {
      id: string
      name: string
      contentTemplate: string
      createdAt: string
    }
    CreateTemplateDto: { name: string; contentTemplate: string }
    UpdateTemplateDto: { name: string; contentTemplate: string }

    MatchedTicketDto: { key: string; summary: string; status: string }
    JiraOnlyTicketDto: { key: string; summary: string }
    GitOnlyTicketDto: { ticketId: string; title: string; commitCount: number }
    ReconciliationResultDto: {
      releaseId: string
      runAt: string
      matchedCount: number
      jiraOnlyCount: number
      gitOnlyCount: number
      matchRatePercent: number
      matched: components["schemas"]["MatchedTicketDto"][]
      jiraOnly: components["schemas"]["JiraOnlyTicketDto"][]
      gitOnly: components["schemas"]["GitOnlyTicketDto"][]
    }

    ComparisonCounts: {
      commitCount: number
      gitTicketCount: number
      jiraTicketCount: number
      inBothCount: number
      jiraOnlyCount: number
      gitOnlyCount: number
    }
    TicketSummaryDto: {
      key: string
      summary: string | null
      status: string | null
      statusCategory: string | null
      assigneeAvatarUrl: string | null
      commitCount: number
    }
    CommitSummaryDto: {
      sha: string
      authorName: string
      message: string
    }
    RepoJiraComparisonDto: {
      repositoryId: string
      repositoryName: string
      currentTag: string | null
      nextVersion: string | null
      jiraFixVersionName: string | null
      jiraFixVersionExists: boolean
      supported: boolean
      unsupportedReason: string | null
      counts: components["schemas"]["ComparisonCounts"]
      matchRate: number
      health: "Green" | "Amber" | "Red" | "Unknown"
      inBoth: components["schemas"]["TicketSummaryDto"][]
      jiraOnly: components["schemas"]["TicketSummaryDto"][]
      gitOnly: components["schemas"]["TicketSummaryDto"][]
      unmatchedCommits: components["schemas"]["CommitSummaryDto"][]
      lastSyncedAt: string
    }
    ProjectJiraCoverageDto: {
      projectId: string
      projectName: string
      totalRepoCount: number
      greenRepoCount: number
      attentionRepoCount: number
      projectMatchRate: number
      repos: components["schemas"]["RepoJiraComparisonDto"][]
    }
    AddToFixVersionResultDto: {
      success: boolean
      jiraFixVersionName: string
      fixVersionCreated: boolean
    }

    ProjectTemplateBindingDto: {
      id: string
      projectId: string
      templateId: string
      templateName: string
      kind: "ReleaseNotes" | "Checklist" | "Custom"
      pageTitleTemplate: string
      parentPageId: string | null
      linkFromReleaseNotes: boolean
      sortOrder: number
    }
    CreateBindingRequest: {
      templateId: string
      kind: "ReleaseNotes" | "Checklist" | "Custom"
      pageTitleTemplate: string
      parentPageId?: string | null
      linkFromReleaseNotes: boolean
      sortOrder: number
    }
    UpdateBindingRequest: {
      templateId?: string | null
      kind?: "ReleaseNotes" | "Checklist" | "Custom" | null
      pageTitleTemplate?: string | null
      parentPageId?: string | null
      linkFromReleaseNotes?: boolean | null
      sortOrder?: number | null
    }
    ProjectCustomVariableDto: { key: string; value: string }

    PreparePageRequest: {
      adminOverrideVersion?: string | null
      reconciliationData?: components["schemas"]["ReconciliationSummaryDto"] | null
    }
    ReconciliationSummaryDto: {
      matchedCount: number
      jiraOnlyCount: number
      gitOnlyCount: number
      matchRate: number
      runAt: string
    }
    PreparedPageDto: {
      bindingId: string
      kind: string
      title: string
      body: string
      parentPageId: string | null
      linkFromReleaseNotes: boolean
      sortOrder: number
      unknownTokens: string[]
    }
    PreparedReleaseDto: {
      context: components["schemas"]["ReleaseRenderContextDto"]
      pages: components["schemas"]["PreparedPageDto"][]
      warnings: string[]
    }
    ReleaseRenderContextDto: {
      project: { id: string; name: string; description: string | null }
      version: string
      previousVersion: string
      releaseDate: string
      repositories: Array<{
        name: string
        previousTag: string
        nextTag: string
        commitCount: number
        ticketCount: number
        jiraFixVersion: string
      }>
      tickets: {
        breaking: components["schemas"]["TicketRenderDto"][]
        features: components["schemas"]["TicketRenderDto"][]
        fixes: components["schemas"]["TicketRenderDto"][]
        other: components["schemas"]["TicketRenderDto"][]
      }
      contributors: Array<{ name: string; email: string; commitCount: number }>
      reconciliation: components["schemas"]["ReconciliationSummaryDto"] | null
      confluence: { spaceKey: string; parentPageId: string }
      custom: Record<string, string>
    }
    TicketRenderDto: { id: string; summary: string; type: string; isBreaking: boolean }
    PublishPagesRequest: { pages: components["schemas"]["PublishPageDto"][] }
    PublishPageDto: {
      bindingId: string
      title: string
      body: string
      parentPageId: string | null
      linkFromReleaseNotes: boolean
      sortOrder: number
    }
    PublishResultDto: { publishedPages: components["schemas"]["PublishedPageDto"][] }
    PublishedPageDto: {
      bindingId: string
      confluencePageId: string
      confluenceUrl: string
      title: string
    }

    ContributorSnapshotDto: { name: string; email: string; commits: number }
    ProjectSyncDto: {
      id: string
      projectId: string
      status: "Pending" | "InProgress" | "Succeeded" | "PartiallyFailed" | "Failed" | "Cancelled"
      startedAt: string
      completedAt: string | null
      totalRepos: number
      succeededCount: number
      failedCount: number
      skippedCount: number
      triggeredByUserId: string
      childSyncs: components["schemas"]["RepositorySyncDto"][] | null
    }
    RepoSyncSnapshotItemDto: {
      repositoryId: string
      repositoryName: string
      latestTag: string | null
      latestSync: components["schemas"]["RepositorySyncDto"] | null
      currentStep: string | null
    }
    RepositorySyncDto: {
      id: string
      repositoryId: string
      projectSyncId: string | null
      fromTag: string
      toCommitSha: string | null
      status: "Pending" | "InProgress" | "Succeeded" | "Failed" | "Skipped"
      skipReason: string | null
      currentStep: string | null
      startedAt: string
      completedAt: string | null
      commitCount: number
      ticketCount: number
      contributorCount: number
      breakingChangeCount: number
      contributors: components["schemas"]["ContributorSnapshotDto"][]
      errorMessage: string | null
    }
  }
  responses: never
  parameters: never
  requestBodies: never
  headers: never
  pathItems: never
}

export type $defs = Record<string, never>
export type operations = Record<string, never>
