import React, { useEffect, useMemo, useState } from 'react';
import { createRoot } from 'react-dom/client';
import './styles.css';

const authStorageKey = 'releaseship-admin-auth';

function getRoute() {
  const hash = window.location.hash.replace(/^#/, '');
  return hash || '/';
}

function setRoute(route) {
  window.location.hash = route;
}

function getStoredAuth() {
  return localStorage.getItem(authStorageKey) ?? '';
}

async function apiFetch(path, options = {}, auth = '') {
  const headers = new Headers(options.headers ?? {});
  if (auth) {
    headers.set('Authorization', `Basic ${auth}`);
  }

  if (options.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetch(path, {
    ...options,
    headers,
  });

  let payload = null;
  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('application/json')) {
    payload = await response.json();
  } else {
    payload = await response.text();
  }

  if (!response.ok) {
    const message =
      payload?.errors?.[0]?.message ??
      payload?.title ??
      payload?.message ??
      payload ??
      `Request failed with ${response.status}`;
    throw new Error(message);
  }

  return payload;
}

function NavLink({ route, children }) {
  return (
    <button className="nav-link" onClick={() => setRoute(route)}>
      {children}
    </button>
  );
}

function Section({ title, children }) {
  return (
    <section className="panel">
      <h2>{title}</h2>
      {children}
    </section>
  );
}

function HomePage({ namespaces, projects }) {
  return (
    <div className="grid">
      <Section title="Public namespaces">
        {namespaces.length === 0 ? <p>No public container namespaces yet.</p> : null}
        <ul>
          {namespaces.map((name) => (
            <li key={name}>
              <button className="inline-link" onClick={() => setRoute(`/containers?namespace=${encodeURIComponent(name)}`)}>
                {name}
              </button>
            </li>
          ))}
        </ul>
      </Section>

      <Section title="Packages">
        {projects.length === 0 ? <p>No packages published yet.</p> : null}
        <ul>
          {projects.map((project) => (
            <li key={project.id}>
              <button className="inline-link" onClick={() => setRoute(`/packages?project=${encodeURIComponent(project.id)}`)}>
                {project.name} <span className="muted">({project.id})</span>
              </button>
            </li>
          ))}
        </ul>
      </Section>
    </div>
  );
}

function ContainersPage({ namespaces }) {
  const params = new URLSearchParams(getRoute().split('?')[1] ?? '');
  const [selectedNamespace, setSelectedNamespace] = useState(params.get('namespace') ?? '');
  const [repositories, setRepositories] = useState([]);
  const [error, setError] = useState('');

  useEffect(() => {
    const query = selectedNamespace ? `?namespace=${encodeURIComponent(selectedNamespace)}` : '';
    apiFetch(`/api/public/registry/repositories${query}`)
      .then(setRepositories)
      .catch((ex) => setError(ex.message));
  }, [selectedNamespace]);

  return (
    <div className="grid">
      <Section title="Namespaces">
        <button className={!selectedNamespace ? 'selected' : ''} onClick={() => setSelectedNamespace('')}>
          All public containers
        </button>
        {namespaces.map((name) => (
          <button
            key={name}
            className={selectedNamespace === name ? 'selected' : ''}
            onClick={() => setSelectedNamespace(name)}
          >
            {name}
          </button>
        ))}
      </Section>

      <Section title="Public container repositories">
        {error ? <p className="error">{error}</p> : null}
        {repositories.length === 0 ? <p>No public repositories found.</p> : null}
        <ul>
          {repositories.map((repository) => (
            <li key={repository.fullName}>
              <strong>{repository.fullName}</strong>
              <div className="muted">{repository.description || 'No description'}</div>
              <div className="badge-row">
                <span className="badge">{repository.defaultTagMutability}</span>
                {repository.allowDelete ? <span className="badge">delete enabled</span> : null}
              </div>
            </li>
          ))}
        </ul>
      </Section>
    </div>
  );
}

function PackagesPage({ projects }) {
  const params = new URLSearchParams(getRoute().split('?')[1] ?? '');
  const [selectedProject, setSelectedProject] = useState(params.get('project') ?? projects[0]?.id ?? '');
  const [tags, setTags] = useState([]);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!selectedProject) {
      setTags([]);
      return;
    }

    apiFetch(`/projects/${selectedProject}/tags`)
      .then(setTags)
      .catch((ex) => setError(ex.message));
  }, [selectedProject]);

  return (
    <div className="grid">
      <Section title="Projects">
        {projects.map((project) => (
          <button
            key={project.id}
            className={selectedProject === project.id ? 'selected' : ''}
            onClick={() => setSelectedProject(project.id)}
          >
            {project.name}
          </button>
        ))}
      </Section>

      <Section title="Published package tags">
        {error ? <p className="error">{error}</p> : null}
        {selectedProject ? <p className="muted">Project: {selectedProject}</p> : null}
        {tags.length === 0 ? <p>No package tags found.</p> : null}
        <ul>
          {tags.map((tag) => (
            <li key={`${tag.id}-${tag.platformId}-${tag.binReleaseId}`}>
              <strong>{tag.id}</strong> <span className="muted">{tag.platformId}</span>
            </li>
          ))}
        </ul>
      </Section>
    </div>
  );
}

function AdminPage() {
  const [auth, setAuth] = useState(getStoredAuth());
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [me, setMe] = useState(null);
  const [repositories, setRepositories] = useState([]);
  const [selectedRepository, setSelectedRepository] = useState('');
  const [tags, setTags] = useState([]);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [repoForm, setRepoForm] = useState({
    fullName: '',
    description: '',
    allowAnonymousPull: true,
    allowDelete: true,
    defaultTagMutability: 'mutable',
    protectedTagPatterns: 'latest',
  });
  const [tokenForm, setTokenForm] = useState({
    name: '',
    scopeType: 'repository',
    scopeValue: '',
    canPull: true,
    canPush: true,
    canDelete: false,
  });
  const [issuedToken, setIssuedToken] = useState(null);

  const isAuthenticated = Boolean(me?.isAdmin);

  async function refreshAdminData(currentAuth = auth) {
    const meResult = await apiFetch('/api/admin/auth/me', {}, currentAuth);
    const repoList = await apiFetch('/api/admin/registry/repositories', {}, currentAuth);
    setMe(meResult);
    setRepositories(repoList);
    if (!selectedRepository && repoList.length > 0) {
      setSelectedRepository(repoList[0].fullName);
      setTokenForm((current) => ({ ...current, scopeValue: repoList[0].fullName }));
    }
  }

  useEffect(() => {
    if (!auth) {
      return;
    }

    refreshAdminData(auth).catch((ex) => {
      setError(ex.message);
      setMe(null);
    });
  }, [auth]);

  useEffect(() => {
    if (!isAuthenticated || !selectedRepository) {
      setTags([]);
      return;
    }

    apiFetch(`/api/admin/registry/tags?repositoryName=${encodeURIComponent(selectedRepository)}`, {}, auth)
      .then(setTags)
      .catch((ex) => setError(ex.message));
  }, [auth, isAuthenticated, selectedRepository]);

  async function handleLogin(event) {
    event.preventDefault();
    setError('');
    const encoded = btoa(`${username}:${password}`);
    try {
      await refreshAdminData(encoded);
      localStorage.setItem(authStorageKey, encoded);
      setAuth(encoded);
      setMessage('Authenticated as admin.');
    } catch (ex) {
      setError(ex.message);
    }
  }

  async function handleRepositorySave(event) {
    event.preventDefault();
    setError('');
    setMessage('');
    try {
      await apiFetch(`/api/admin/registry/repositories/${repoForm.fullName}`, {
        method: 'PUT',
        body: JSON.stringify({
          description: repoForm.description,
          allowAnonymousPull: repoForm.allowAnonymousPull,
          allowDelete: repoForm.allowDelete,
          defaultTagMutability: repoForm.defaultTagMutability,
          protectedTagPatterns: repoForm.protectedTagPatterns
            .split(',')
            .map((item) => item.trim())
            .filter(Boolean),
        }),
      }, auth);
      await refreshAdminData();
      setSelectedRepository(repoForm.fullName);
      setTokenForm((current) => ({ ...current, scopeValue: repoForm.fullName }));
      setMessage(`Saved repository ${repoForm.fullName}.`);
    } catch (ex) {
      setError(ex.message);
    }
  }

  async function handleIssueToken(event) {
    event.preventDefault();
    setError('');
    setMessage('');
    try {
      const token = await apiFetch('/api/admin/auth/tokens', {
        method: 'POST',
        body: JSON.stringify(tokenForm),
      }, auth);
      setIssuedToken(token);
      setMessage(`Issued token ${token.name}. Copy the secret now.`);
    } catch (ex) {
      setError(ex.message);
    }
  }

  async function handleDeleteTag(tag) {
    setError('');
    setMessage('');
    try {
      await apiFetch(`/api/admin/registry/tags?repositoryName=${encodeURIComponent(selectedRepository)}&tagName=${encodeURIComponent(tag.name)}`, {
        method: 'DELETE',
      }, auth);
      const updated = await apiFetch(`/api/admin/registry/tags?repositoryName=${encodeURIComponent(selectedRepository)}`, {}, auth);
      setTags(updated);
      setMessage(`Deleted tag ${tag.name}.`);
    } catch (ex) {
      setError(ex.message);
    }
  }

  if (!isAuthenticated) {
    return (
      <Section title="Admin login">
        <form className="stack" onSubmit={handleLogin}>
          <label>
            Username
            <input value={username} onChange={(event) => setUsername(event.target.value)} />
          </label>
          <label>
            Password
            <input type="password" value={password} onChange={(event) => setPassword(event.target.value)} />
          </label>
          <button type="submit">Sign in</button>
        </form>
        {error ? <p className="error">{error}</p> : null}
      </Section>
    );
  }

  return (
    <div className="grid">
      <Section title="Repository management">
        <form className="stack" onSubmit={handleRepositorySave}>
          <label>
            Full name
            <input value={repoForm.fullName} onChange={(event) => setRepoForm({ ...repoForm, fullName: event.target.value })} />
          </label>
          <label>
            Description
            <input value={repoForm.description} onChange={(event) => setRepoForm({ ...repoForm, description: event.target.value })} />
          </label>
          <label>
            Default tag mutability
            <select value={repoForm.defaultTagMutability} onChange={(event) => setRepoForm({ ...repoForm, defaultTagMutability: event.target.value })}>
              <option value="mutable">mutable</option>
              <option value="immutable">immutable</option>
            </select>
          </label>
          <label>
            Protected tag patterns
            <input value={repoForm.protectedTagPatterns} onChange={(event) => setRepoForm({ ...repoForm, protectedTagPatterns: event.target.value })} />
          </label>
          <label className="checkbox">
            <input
              type="checkbox"
              checked={repoForm.allowAnonymousPull}
              onChange={(event) => setRepoForm({ ...repoForm, allowAnonymousPull: event.target.checked })}
            />
            Allow anonymous pull
          </label>
          <label className="checkbox">
            <input
              type="checkbox"
              checked={repoForm.allowDelete}
              onChange={(event) => setRepoForm({ ...repoForm, allowDelete: event.target.checked })}
            />
            Allow delete
          </label>
          <button type="submit">Save repository</button>
        </form>

        <ul>
          {repositories.map((repository) => (
            <li key={repository.fullName}>
              <button
                className={selectedRepository === repository.fullName ? 'selected' : ''}
                onClick={() => {
                  setSelectedRepository(repository.fullName);
                  setRepoForm({
                    fullName: repository.fullName,
                    description: repository.description ?? '',
                    allowAnonymousPull: repository.allowAnonymousPull,
                    allowDelete: repository.allowDelete,
                    defaultTagMutability: repository.defaultTagMutability,
                    protectedTagPatterns: (repository.protectedTagPatterns ?? []).join(','),
                  });
                  setTokenForm((current) => ({ ...current, scopeValue: repository.fullName }));
                }}
              >
                {repository.fullName}
              </button>
            </li>
          ))}
        </ul>
      </Section>

      <Section title="Repository tags">
        <p className="muted">{selectedRepository || 'Select a repository'}</p>
        <ul>
          {tags.map((tag) => (
            <li key={tag.name}>
              <strong>{tag.name}</strong>
              <div className="muted">{tag.manifestDigest}</div>
              <button className="danger" onClick={() => handleDeleteTag(tag)}>
                Delete tag
              </button>
            </li>
          ))}
        </ul>
      </Section>

      <Section title="Issue token">
        <form className="stack" onSubmit={handleIssueToken}>
          <label>
            Token name
            <input value={tokenForm.name} onChange={(event) => setTokenForm({ ...tokenForm, name: event.target.value })} />
          </label>
          <label>
            Scope type
            <select value={tokenForm.scopeType} onChange={(event) => setTokenForm({ ...tokenForm, scopeType: event.target.value })}>
              <option value="repository">repository</option>
              <option value="global">global</option>
            </select>
          </label>
          <label>
            Scope value
            <input value={tokenForm.scopeValue} onChange={(event) => setTokenForm({ ...tokenForm, scopeValue: event.target.value })} />
          </label>
          <label className="checkbox">
            <input type="checkbox" checked={tokenForm.canPull} onChange={(event) => setTokenForm({ ...tokenForm, canPull: event.target.checked })} />
            Pull
          </label>
          <label className="checkbox">
            <input type="checkbox" checked={tokenForm.canPush} onChange={(event) => setTokenForm({ ...tokenForm, canPush: event.target.checked })} />
            Push
          </label>
          <label className="checkbox">
            <input type="checkbox" checked={tokenForm.canDelete} onChange={(event) => setTokenForm({ ...tokenForm, canDelete: event.target.checked })} />
            Delete
          </label>
          <button type="submit">Issue token</button>
        </form>
        {issuedToken ? (
          <div className="token-box">
            <div><strong>{issuedToken.name}</strong></div>
            <div className="muted">Secret</div>
            <code>{issuedToken.secret}</code>
          </div>
        ) : null}
      </Section>

      {message ? <p className="success">{message}</p> : null}
      {error ? <p className="error">{error}</p> : null}
    </div>
  );
}

function App() {
  const [route, setRouteState] = useState(getRoute());
  const [namespaces, setNamespaces] = useState([]);
  const [projects, setProjects] = useState([]);

  useEffect(() => {
    const onHashChange = () => setRouteState(getRoute());
    window.addEventListener('hashchange', onHashChange);
    return () => window.removeEventListener('hashchange', onHashChange);
  }, []);

  useEffect(() => {
    apiFetch('/api/public/registry/namespaces').then((items) => setNamespaces(items.map((item) => item.name)));
    apiFetch('/projects').then(setProjects);
  }, []);

  const page = useMemo(() => route.split('?')[0], [route]);

  return (
    <div className="app-shell">
      <header>
        <h1>ReleaseShip</h1>
        <p className="muted">Public browsing for packages and containers, plus authenticated registry management.</p>
        <nav className="nav">
          <NavLink route="/">Home</NavLink>
          <NavLink route="/containers">Containers</NavLink>
          <NavLink route="/packages">Packages</NavLink>
          <NavLink route="/admin">Admin</NavLink>
        </nav>
      </header>

      <main>
        {page === '/' ? <HomePage namespaces={namespaces} projects={projects} /> : null}
        {page === '/containers' ? <ContainersPage namespaces={namespaces} /> : null}
        {page === '/packages' ? <PackagesPage projects={projects} /> : null}
        {page === '/admin' ? <AdminPage /> : null}
      </main>
    </div>
  );
}

createRoot(document.getElementById('root')).render(<App />);
