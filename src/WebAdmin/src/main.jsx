import React, { useCallback, useEffect, useMemo, useState } from 'react';
import ReactDOM from 'react-dom/client';
import './styles.css';

function authHeaders(auth) {
  return auth
    ? {
        Authorization: `Basic ${auth}`,
      }
    : {};
}

async function apiFetch(url, options = {}, auth) {
  const response = await fetch(url, {
    ...options,
    headers: {
      Accept: 'application/json',
      ...(options.body ? { 'Content-Type': 'application/json' } : {}),
      ...authHeaders(auth),
      ...(options.headers ?? {}),
    },
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Request failed with status ${response.status}`);
  }

  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('application/json')) {
    return response.json();
  }

  return null;
}

function currentRoute() {
  const url = new URL(window.location.href);
  const path = url.pathname === '/' ? '/' : url.pathname.replace(/\/+$/, '');
  return {
    path: path || '/',
    params: url.searchParams,
  };
}

function navigate(path, params) {
  const url = new URL(window.location.origin);
  url.pathname = path;

  if (params) {
    for (const [key, value] of Object.entries(params)) {
      if (value) {
        url.searchParams.set(key, value);
      }
    }
  }

  window.history.pushState({}, '', `${url.pathname}${url.search}`);
  window.dispatchEvent(new PopStateEvent('popstate'));
}

function App() {
  const [route, setRoute] = useState(() => currentRoute());
  const [auth, setAuth] = useState(() => window.localStorage.getItem('releaseShipAuth') ?? '');
  const [me, setMe] = useState(null);
  const [sessionReady, setSessionReady] = useState(false);
  const [loginOpen, setLoginOpen] = useState(false);
  const [loginUsername, setLoginUsername] = useState('');
  const [loginPassword, setLoginPassword] = useState('');
  const [loginError, setLoginError] = useState('');
  const [loginBusy, setLoginBusy] = useState(false);

  useEffect(() => {
    const onPopState = () => setRoute(currentRoute());
    window.addEventListener('popstate', onPopState);
    return () => window.removeEventListener('popstate', onPopState);
  }, []);

  const loadSession = useCallback(async (encodedAuth) => {
    if (!encodedAuth) {
      setMe(null);
      setSessionReady(true);
      return;
    }

    try {
      const profile = await apiFetch('/api/admin/auth/me', {}, encodedAuth);
      setMe(profile);
    } catch {
      window.localStorage.removeItem('releaseShipAuth');
      setAuth('');
      setMe(null);
    } finally {
      setSessionReady(true);
    }
  }, []);

  useEffect(() => {
    setSessionReady(false);
    loadSession(auth);
  }, [auth, loadSession]);

  async function submitLogin(event) {
    event.preventDefault();
    setLoginBusy(true);
    setLoginError('');

    try {
      const encoded = window.btoa(`${loginUsername}:${loginPassword}`);
      const profile = await apiFetch('/api/admin/auth/me', {}, encoded);
      window.localStorage.setItem('releaseShipAuth', encoded);
      setAuth(encoded);
      setMe(profile);
      setLoginPassword('');
      setLoginOpen(false);
    } catch (error) {
      setLoginError(error.message);
    } finally {
      setLoginBusy(false);
    }
  }

  function signOut() {
    window.localStorage.removeItem('releaseShipAuth');
    setAuth('');
    setMe(null);
    setLoginPassword('');
    setLoginError('');
    if (route.path === '/operations') {
      navigate('/');
    }
  }

  const isAdmin = Boolean(me?.isAdmin);
  const routeState = useMemo(() => {
    if (route.path === '/containers') {
      return {
        namespace: route.params.get('namespace') ?? '',
      };
    }

    if (route.path === '/packages') {
      return {
        project: route.params.get('project') ?? '',
      };
    }

    return {};
  }, [route]);

  let content = null;
  if (route.path === '/containers') {
    content = (
      <ContainerCatalogPage
        selectedNamespace={routeState.namespace}
        onSelectNamespace={(namespace) => navigate('/containers', namespace ? { namespace } : undefined)}
      />
    );
  } else if (route.path === '/packages') {
    content = (
      <PackageCatalogPage
        selectedProject={routeState.project}
        onSelectProject={(project) => navigate('/packages', project ? { project } : undefined)}
      />
    );
  } else if (route.path === '/operations') {
    content = (
      <OperationsPage
        auth={auth}
        isAdmin={isAdmin}
        me={me}
        sessionReady={sessionReady}
        onOpenLogin={() => setLoginOpen(true)}
      />
    );
  } else {
    content = (
      <HomePage
        isAdmin={isAdmin}
        me={me}
        onBrowseContainers={() => navigate('/containers')}
        onBrowsePackages={() => navigate('/packages')}
        onOpenOperations={() => navigate('/operations')}
        onOpenLogin={() => setLoginOpen(true)}
      />
    );
  }

  return (
    <div className="app-shell">
      <header className="app-bar">
        <button className="brand" onClick={() => navigate('/')}>
          <span className="brand-mark">RS</span>
          <span>
            <strong>ReleaseShip</strong>
            <small>Offline artifact and container delivery</small>
          </span>
        </button>

        <nav className="app-nav">
          <NavButton active={route.path === '/'} label="Overview" onClick={() => navigate('/')} />
          <NavButton active={route.path === '/containers'} label="Containers" onClick={() => navigate('/containers')} />
          <NavButton active={route.path === '/packages'} label="Packages" onClick={() => navigate('/packages')} />
          <NavButton active={route.path === '/operations'} label="Operations" onClick={() => navigate('/operations')} />
        </nav>

        <div className="app-bar-actions">
          {isAdmin ? (
            <>
              <div className="identity-pill">
                <span className="status-dot" />
                {me.username}
              </div>
              <button className="secondary-button" onClick={signOut}>
                Sign out
              </button>
            </>
          ) : (
            <button className="primary-button" onClick={() => setLoginOpen(true)}>
              Sign in
            </button>
          )}
        </div>
      </header>

      <main className="page-shell">{content}</main>

      {loginOpen ? (
        <div className="modal-backdrop" role="presentation" onClick={() => setLoginOpen(false)}>
          <div className="modal-card" role="dialog" aria-modal="true" onClick={(event) => event.stopPropagation()}>
            <div className="modal-header">
              <div>
                <h2>Sign in to ReleaseShip</h2>
                <p>Use your bootstrap admin credentials to unlock repository and token management.</p>
              </div>
              <button className="icon-button" onClick={() => setLoginOpen(false)} aria-label="Close sign in dialog">
                x
              </button>
            </div>

            <form className="stack" onSubmit={submitLogin}>
              <label>
                Username
                <input value={loginUsername} onChange={(event) => setLoginUsername(event.target.value)} autoComplete="username" />
              </label>
              <label>
                Password
                <input
                  type="password"
                  value={loginPassword}
                  onChange={(event) => setLoginPassword(event.target.value)}
                  autoComplete="current-password"
                />
              </label>

              {loginError ? <p className="error-banner">{loginError}</p> : null}

              <div className="modal-actions">
                <button type="button" className="secondary-button" onClick={() => setLoginOpen(false)}>
                  Cancel
                </button>
                <button type="submit" className="primary-button" disabled={loginBusy}>
                  {loginBusy ? 'Signing in...' : 'Sign in'}
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function NavButton({ active, label, onClick }) {
  return (
    <button className={`nav-button${active ? ' nav-button-active' : ''}`} onClick={onClick}>
      {label}
    </button>
  );
}

function HomePage({ isAdmin, me, onBrowseContainers, onBrowsePackages, onOpenOperations, onOpenLogin }) {
  return (
    <div className="stack page-gap">
      <section className="hero-panel">
        <div className="hero-copy">
          <div className="eyebrow">Unified delivery surface</div>
          <h1>One polished front door for packages, OCI images, and operational controls.</h1>
          <p>
            ReleaseShip now serves binary artifacts and container repositories from the same host, with scoped auth, protected tags,
            and a lightweight management experience built for offline environments.
          </p>
          <div className="hero-actions">
            <button className="primary-button" onClick={onBrowseContainers}>
              Explore containers
            </button>
            <button className="secondary-button" onClick={onBrowsePackages}>
              Browse packages
            </button>
            {isAdmin ? (
              <button className="ghost-button" onClick={onOpenOperations}>
                Open operations
              </button>
            ) : (
              <button className="ghost-button" onClick={onOpenLogin}>
                Sign in for operations
              </button>
            )}
          </div>
        </div>

        <div className="hero-metrics">
          <MetricCard title="Registry-ready" value="OCI v2" detail="Push, pull, tag policy, and delete controls in one host." />
          <MetricCard title="Auth model" value="Scoped" detail="Bootstrap admin plus repository or global tokens." />
          <MetricCard title="Deployment" value="Offline" detail="Filesystem-first storage with extensible backends." />
        </div>
      </section>

      <section className="section-grid">
        <InfoCard
          title="Container registry"
          description="Browse namespaces, inspect repository policy, and publish OCI-compatible images."
          actionLabel="View container catalog"
          onAction={onBrowseContainers}
        />
        <InfoCard
          title="Package archive"
          description="Continue serving binary artifacts with the existing project and release inventory."
          actionLabel="View package catalog"
          onAction={onBrowsePackages}
        />
        <InfoCard
          title="Operations"
          description={
            isAdmin ? `Signed in as ${me.username}. Manage repository policy, tokens, and tag operations.` : 'Sign in from the app bar to manage repositories and issue scoped credentials.'
          }
          actionLabel={isAdmin ? 'Open operations' : 'Sign in'}
          onAction={isAdmin ? onOpenOperations : onOpenLogin}
        />
      </section>
    </div>
  );
}

function MetricCard({ title, value, detail }) {
  return (
    <article className="metric-card">
      <span>{title}</span>
      <strong>{value}</strong>
      <p>{detail}</p>
    </article>
  );
}

function InfoCard({ title, description, actionLabel, onAction }) {
  return (
    <article className="panel-card">
      <div className="panel-card-copy">
        <h2>{title}</h2>
        <p>{description}</p>
      </div>
      <button className="secondary-button" onClick={onAction}>
        {actionLabel}
      </button>
    </article>
  );
}

function ContainerCatalogPage({ selectedNamespace, onSelectNamespace }) {
  const [namespaces, setNamespaces] = useState([]);
  const [repositories, setRepositories] = useState([]);
  const [error, setError] = useState('');

  useEffect(() => {
    let cancelled = false;

    apiFetch('/api/public/registry/namespaces')
      .then((payload) => {
        if (!cancelled) {
          setNamespaces(payload ?? []);
          setError('');
        }
      })
      .catch((apiError) => {
        if (!cancelled) {
          setError(apiError.message);
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const activeNamespace = namespaces.find((entry) => entry.name === selectedNamespace) ?? namespaces[0] ?? null;

  useEffect(() => {
    if (!activeNamespace?.name) {
      setRepositories([]);
      return;
    }

    let cancelled = false;

    apiFetch(`/api/public/registry/repositories?namespace=${encodeURIComponent(activeNamespace.name)}`)
      .then((payload) => {
        if (!cancelled) {
          setRepositories(payload ?? []);
          setError('');
        }
      })
      .catch((apiError) => {
        if (!cancelled) {
          setError(apiError.message);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [activeNamespace?.name]);

  return (
    <div className="stack page-gap">
      <section className="page-header">
        <div>
          <div className="eyebrow">Container catalog</div>
          <h1>Public namespaces and repositories</h1>
          <p>Browse repositories that are visible to public users, including anonymous-pull container images.</p>
        </div>
      </section>

      {error ? <p className="error-banner">{error}</p> : null}

      <div className="split-layout">
        <section className="sidebar-card">
          <h2>Namespaces</h2>
          <div className="list-stack">
            {namespaces.map((namespace) => (
              <button
                key={namespace.name}
                className={`list-button${activeNamespace?.name === namespace.name ? ' list-button-active' : ''}`}
                onClick={() => onSelectNamespace(namespace.name)}
              >
                <span>{namespace.name}</span>
                <small>Namespace</small>
              </button>
            ))}
            {!namespaces.length ? <p className="muted-copy">No public container namespaces are currently available.</p> : null}
          </div>
        </section>

        <section className="content-card">
          <div className="content-card-header">
            <div>
              <h2>{activeNamespace ? activeNamespace.name : 'Select a namespace'}</h2>
              <p>{activeNamespace ? `${repositories.length} repositories published in this namespace.` : 'Choose a namespace to inspect its repositories.'}</p>
            </div>
          </div>

          <div className="list-stack">
            {repositories.map((repository) => (
              <article key={repository.fullName} className="resource-card">
                <div>
                  <strong>{repository.fullName}</strong>
                  <p>{repository.description || 'No description provided.'}</p>
                </div>
                <div className="tag-row">
                  <span className="tag-chip">{repository.defaultTagMutability}</span>
                  {repository.allowAnonymousPull ? <span className="tag-chip tag-chip-accent">Anonymous pull</span> : null}
                </div>
              </article>
            ))}
            {!repositories.length ? <p className="muted-copy">No repositories are visible in this namespace yet.</p> : null}
          </div>
        </section>
      </div>
    </div>
  );
}

function PackageCatalogPage({ selectedProject, onSelectProject }) {
  const [projects, setProjects] = useState([]);
  const [tags, setTags] = useState([]);
  const [error, setError] = useState('');

  useEffect(() => {
    let cancelled = false;

    apiFetch('/projects')
      .then((payload) => {
        if (!cancelled) {
          setProjects(payload ?? []);
          setError('');
        }
      })
      .catch((apiError) => {
        if (!cancelled) {
          setError(apiError.message);
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const activeProject = projects.find((entry) => entry.id === selectedProject) ?? projects[0] ?? null;

  useEffect(() => {
    if (!activeProject?.id) {
      setTags([]);
      return;
    }

    let cancelled = false;

    apiFetch(`/projects/${encodeURIComponent(activeProject.id)}/tags`)
      .then((payload) => {
        if (!cancelled) {
          setTags(payload ?? []);
          setError('');
        }
      })
      .catch((apiError) => {
        if (!cancelled) {
          setError(apiError.message);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [activeProject?.id]);

  return (
    <div className="stack page-gap">
      <section className="page-header">
        <div>
          <div className="eyebrow">Package catalog</div>
          <h1>Binary artifacts and published releases</h1>
          <p>Inspect projects and browse the release history already served by ReleaseShip.</p>
        </div>
      </section>

      {error ? <p className="error-banner">{error}</p> : null}

      <div className="split-layout">
        <section className="sidebar-card">
          <h2>Projects</h2>
          <div className="list-stack">
            {projects.map((project) => (
              <button
                key={project.id}
                className={`list-button${activeProject?.id === project.id ? ' list-button-active' : ''}`}
                onClick={() => onSelectProject(project.id)}
              >
                <span>{project.name}</span>
                <small>{project.id}</small>
              </button>
            ))}
            {!projects.length ? <p className="muted-copy">No projects were found.</p> : null}
          </div>
        </section>

        <section className="content-card">
          <div className="content-card-header">
            <div>
              <h2>{activeProject ? activeProject.name : 'Select a project'}</h2>
              <p>{activeProject ? `${tags.length} published release tags.` : 'Choose a project to see release details.'}</p>
            </div>
          </div>

          {activeProject ? (
            <div className="list-stack">
              <article className="resource-card">
                <div>
                  <strong>{activeProject.name}</strong>
                  <p>Project id: {activeProject.id}</p>
                </div>
                <div className="tag-row">
                  <span className="tag-chip">{tags.length} releases</span>
                </div>
              </article>

              {tags.map((tag) => (
                <article key={`${tag.id}-${tag.platformId}`} className="resource-card">
                  <div>
                    <strong>{tag.id}</strong>
                    <p>{tag.platformId}</p>
                  </div>
                  <div className="tag-row">
                    <span className="tag-chip">{new Date(tag.createDateUtc).toLocaleString()}</span>
                  </div>
                </article>
              ))}

              {!tags.length ? <p className="muted-copy">No release tags are currently published for this project.</p> : null}
            </div>
          ) : (
            <p className="muted-copy">No project selected.</p>
          )}
        </section>
      </div>
    </div>
  );
}

function OperationsPage({ auth, isAdmin, me, onOpenLogin, sessionReady }) {
  const [repositories, setRepositories] = useState([]);
  const [selectedRepository, setSelectedRepository] = useState('');
  const [tags, setTags] = useState([]);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [repoForm, setRepoForm] = useState({
    fullName: '',
    description: '',
    allowAnonymousPull: false,
    allowDelete: false,
    defaultTagMutability: 'mutable',
    protectedPatterns: '',
  });
  const [tokenForm, setTokenForm] = useState({
    name: '',
    scopeType: 'repository',
    repositoryName: '',
    canPull: true,
    canPush: true,
    canDelete: false,
  });

  const refreshRepositories = useCallback(async () => {
    const payload = await apiFetch('/api/admin/registry/repositories', {}, auth);
    setRepositories(payload ?? []);
    return payload ?? [];
  }, [auth]);

  const refreshTags = useCallback(
    async (repositoryName) => {
      if (!repositoryName) {
        setTags([]);
        return;
      }

      const payload = await apiFetch(`/api/admin/registry/tags?repositoryName=${encodeURIComponent(repositoryName)}`, {}, auth);
      setTags(payload ?? []);
    },
    [auth],
  );

  useEffect(() => {
    if (!isAdmin) {
      return;
    }

    let cancelled = false;

    refreshRepositories()
      .then((items) => {
        if (!cancelled && items.length) {
          const first = items[0];
          setSelectedRepository((current) => current || first.fullName);
          setRepoForm({
            fullName: first.fullName,
            description: first.description ?? '',
            allowAnonymousPull: Boolean(first.allowAnonymousPull),
            allowDelete: Boolean(first.allowDelete),
            defaultTagMutability: first.defaultTagMutability ?? 'mutable',
            protectedPatterns: (first.protectedTagPatterns ?? []).join('\n'),
          });
          setTokenForm((current) => ({
            ...current,
            repositoryName: current.repositoryName || first.fullName,
          }));
        }
      })
      .catch((apiError) => {
        if (!cancelled) {
          setError(apiError.message);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [isAdmin, refreshRepositories]);

  useEffect(() => {
    if (!isAdmin || !selectedRepository) {
      return;
    }

    refreshTags(selectedRepository).catch((apiError) => setError(apiError.message));
  }, [isAdmin, refreshTags, selectedRepository]);

  function applyRepository(repository) {
    setSelectedRepository(repository.fullName);
    setRepoForm({
      fullName: repository.fullName,
      description: repository.description ?? '',
      allowAnonymousPull: Boolean(repository.allowAnonymousPull),
      allowDelete: Boolean(repository.allowDelete),
      defaultTagMutability: repository.defaultTagMutability ?? 'mutable',
      protectedPatterns: (repository.protectedTagPatterns ?? []).join('\n'),
    });
    setTokenForm((current) => ({
      ...current,
      repositoryName: repository.fullName,
    }));
  }

  async function saveRepository(event) {
    event.preventDefault();
    setError('');
    setMessage('');

    try {
      await apiFetch(
        `/api/admin/registry/repositories/${encodeURIComponent(repoForm.fullName)}`,
        {
          method: 'PUT',
          body: JSON.stringify({
            description: repoForm.description || null,
            allowAnonymousPull: repoForm.allowAnonymousPull,
            allowDelete: repoForm.allowDelete,
            defaultTagMutability: repoForm.defaultTagMutability,
            protectedTagPatterns: repoForm.protectedPatterns
              .split(/\r?\n/)
              .map((entry) => entry.trim())
              .filter(Boolean),
          }),
        },
        auth,
      );

      const items = await refreshRepositories();
      const saved = items.find((entry) => entry.fullName === repoForm.fullName);
      if (saved) {
        applyRepository(saved);
      }

      setMessage(`Saved repository settings for ${repoForm.fullName}.`);
    } catch (apiError) {
      setError(apiError.message);
    }
  }

  async function issueToken(event) {
    event.preventDefault();
    setError('');
    setMessage('');

    try {
      const payload = await apiFetch(
        '/api/admin/auth/tokens',
        {
          method: 'POST',
          body: JSON.stringify({
            name: tokenForm.name,
            scopeType: tokenForm.scopeType,
            scopeValue: tokenForm.scopeType === 'repository' ? tokenForm.repositoryName : '',
            canPull: tokenForm.canPull,
            canPush: tokenForm.canPush,
            canDelete: tokenForm.canDelete,
          }),
        },
        auth,
      );

      setMessage(`Token created. Secret: ${payload.secret}`);
      setTokenForm({
        name: '',
        scopeType: 'repository',
        repositoryName: selectedRepository || tokenForm.repositoryName,
        canPull: true,
        canPush: true,
        canDelete: false,
      });
    } catch (apiError) {
      setError(apiError.message);
    }
  }

  async function deleteTag(tagName) {
    setError('');
    setMessage('');

    try {
      await apiFetch(
        `/v2/${encodeURIComponent(selectedRepository)}/manifests/${encodeURIComponent(tagName)}`,
        {
          method: 'DELETE',
        },
        auth,
      );

      await refreshTags(selectedRepository);
      setMessage(`Deleted tag ${tagName} from ${selectedRepository}.`);
    } catch (apiError) {
      setError(apiError.message);
    }
  }

  if (!sessionReady) {
    return <div className="panel-card">Loading session...</div>;
  }

  if (!isAdmin) {
    return (
      <div className="stack page-gap">
        <section className="hero-panel compact-hero">
          <div className="hero-copy">
            <div className="eyebrow">Operations</div>
            <h1>Authenticated management stays inside the main application shell.</h1>
            <p>Sign in from the app bar to manage repository visibility, tag mutability, and scoped credentials.</p>
            <div className="hero-actions">
              <button className="primary-button" onClick={onOpenLogin}>
                Sign in to continue
              </button>
            </div>
          </div>
        </section>
      </div>
    );
  }

  return (
    <div className="stack page-gap">
      <section className="page-header">
        <div>
          <div className="eyebrow">Operations</div>
          <h1>Registry administration</h1>
          <p>Signed in as {me.username}. Manage repositories, tag policy, and scoped tokens from one workspace.</p>
        </div>
      </section>

      {message ? <p className="success-banner">{message}</p> : null}
      {error ? <p className="error-banner">{error}</p> : null}

      <div className="operations-grid">
        <section className="sidebar-card">
          <h2>Repositories</h2>
          <div className="list-stack">
            {repositories.map((repository) => (
              <button
                key={repository.fullName}
                className={`list-button${selectedRepository === repository.fullName ? ' list-button-active' : ''}`}
                onClick={() => applyRepository(repository)}
              >
                <span>{repository.fullName}</span>
                <small>{repository.defaultTagMutability}</small>
              </button>
            ))}
            {!repositories.length ? <p className="muted-copy">No repositories have been created yet.</p> : null}
          </div>
        </section>

        <div className="stack">
          <section className="content-card">
            <div className="content-card-header">
              <div>
                <h2>Repository settings</h2>
                <p>Control anonymous pull access, tag mutability, delete capability, and protected tag patterns.</p>
              </div>
            </div>

            <form className="stack" onSubmit={saveRepository}>
              <label>
                Full repository name
                <input value={repoForm.fullName} onChange={(event) => setRepoForm({ ...repoForm, fullName: event.target.value })} />
              </label>
              <label>
                Description
                <textarea value={repoForm.description} onChange={(event) => setRepoForm({ ...repoForm, description: event.target.value })} rows="3" />
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={repoForm.allowAnonymousPull}
                  onChange={(event) => setRepoForm({ ...repoForm, allowAnonymousPull: event.target.checked })}
                />
                Allow anonymous pull
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={repoForm.allowDelete}
                  onChange={(event) => setRepoForm({ ...repoForm, allowDelete: event.target.checked })}
                />
                Allow delete operations
              </label>
              <label>
                Default tag mutability
                <select
                  value={repoForm.defaultTagMutability}
                  onChange={(event) => setRepoForm({ ...repoForm, defaultTagMutability: event.target.value })}
                >
                  <option value="mutable">Mutable</option>
                  <option value="immutable">Immutable</option>
                </select>
              </label>
              <label>
                Protected tag patterns
                <textarea
                  value={repoForm.protectedPatterns}
                  onChange={(event) => setRepoForm({ ...repoForm, protectedPatterns: event.target.value })}
                  rows="4"
                  placeholder="release-*&#10;stable"
                />
              </label>
              <div>
                <button className="primary-button" type="submit">
                  Save repository
                </button>
              </div>
            </form>
          </section>

          <section className="content-card">
            <div className="content-card-header">
              <div>
                <h2>Issue scoped token</h2>
                <p>Create repository or global credentials for automation and controlled push/delete operations.</p>
              </div>
            </div>

            <form className="stack" onSubmit={issueToken}>
              <label>
                Token name
                <input value={tokenForm.name} onChange={(event) => setTokenForm({ ...tokenForm, name: event.target.value })} />
              </label>
              <label>
                Scope
                <select value={tokenForm.scopeType} onChange={(event) => setTokenForm({ ...tokenForm, scopeType: event.target.value })}>
                  <option value="repository">Repository</option>
                  <option value="global">Global</option>
                </select>
              </label>
              {tokenForm.scopeType === 'repository' ? (
                <label>
                  Repository
                  <input
                    value={tokenForm.repositoryName}
                    onChange={(event) => setTokenForm({ ...tokenForm, repositoryName: event.target.value })}
                  />
                </label>
              ) : null}
              <label className="checkbox-row">
                <input type="checkbox" checked={tokenForm.canPull} onChange={(event) => setTokenForm({ ...tokenForm, canPull: event.target.checked })} />
                Allow pull
              </label>
              <label className="checkbox-row">
                <input type="checkbox" checked={tokenForm.canPush} onChange={(event) => setTokenForm({ ...tokenForm, canPush: event.target.checked })} />
                Allow push
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={tokenForm.canDelete}
                  onChange={(event) => setTokenForm({ ...tokenForm, canDelete: event.target.checked })}
                />
                Allow delete
              </label>
              <div>
                <button className="primary-button" type="submit">
                  Create token
                </button>
              </div>
            </form>
          </section>
        </div>

        <section className="content-card">
          <div className="content-card-header">
            <div>
              <h2>Tags</h2>
              <p>Inspect repository tag state and remove tags with delete-capable credentials.</p>
            </div>
          </div>

          <div className="list-stack">
            {tags.map((tag) => (
              <article key={tag.name} className="resource-card">
                <div>
                  <strong>{tag.name}</strong>
                  <p>{tag.manifestDigest}</p>
                </div>
                <button className="danger-button" onClick={() => deleteTag(tag.name)}>
                  Delete
                </button>
              </article>
            ))}
            {!tags.length ? <p className="muted-copy">No tags are currently published for this repository.</p> : null}
          </div>
        </section>
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
