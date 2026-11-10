// Runs actual component/service methods with real RxJS/Angular forms and isolated
// injection/HTTP doubles. No browser, production API or new packages are needed.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');
const rx = require('rxjs');

(async () => {
  await import('@angular/compiler');
  const core = await import('@angular/core');
  const http = await import('@angular/common/http');
  const forms = await import('@angular/forms');
  const interop = await import('@angular/core/rxjs-interop');
  const providers = new Map();
  const tokens = Object.fromEntries(['DestroyRef', 'HttpClient', 'Router', 'AuthService', 'ThemeService', 'NotificationService', 'SeriesService', 'ProfileService', 'EpisodioAssistidoService'].map(name => [name, class { static tokenName = name; }]));
  const cache = new Map();
  const storage = { getItem: () => null, setItem() {}, removeItem() {} };
  function load(relative) {
    const filename = path.resolve(__dirname, '../src/app', relative);
    if (cache.has(filename)) return cache.get(filename);
    const exports = {};
    cache.set(filename, exports);
    const context = {
      exports, localStorage: storage, console, Map, Set, Intl,
      require: name => {
        if (name === '@angular/core') return {
          ...core, Component: () => value => value, Injectable: () => value => value,
          ViewChild: () => () => {},
          DestroyRef: tokens.DestroyRef,
          input: Object.assign(value => core.signal(value), { required: () => core.signal(undefined) }),
          output: () => ({ emit() {} }), HostListener: () => () => {},
          viewChild: () => () => undefined, afterRenderEffect() {},
          inject: token => {
            assert(providers.has(token.tokenName), `Missing isolated provider ${token.tokenName}`);
            return providers.get(token.tokenName);
          },
        };
        if (name === '@angular/common/http') return { ...http, HttpClient: tokens.HttpClient };
        if (name === '@angular/core/rxjs-interop') return interop;
        if (name === '@angular/common') return {};
        if (name === '@angular/forms') return forms;
        // RxJS recognizes plain-object forkJoin inputs by their realm's prototype.
        if (name === 'rxjs') return { ...rx, forkJoin: sources => rx.forkJoin({ ...sources }) };
        if (name === '@angular/router') return { Router: tokens.Router };
        if (name.startsWith('primeng/')) return {};
        for (const [tokenName, token] of Object.entries(tokens)) {
          const stem = tokenName.replace(/Service$/, '').replace(/[A-Z]/g, (c, i) => (i ? '-' : '') + c.toLowerCase());
          if (name.endsWith(`/${stem}.service`) || name === `./${stem}.service`) return { [tokenName]: token };
        }
        if (name.startsWith('.')) return load(path.relative(path.resolve(__dirname, '../src/app'), path.resolve(path.dirname(filename), `${name}.ts`)));
        throw new Error(`Unexpected import ${name}`);
      },
    };
    vm.runInNewContext(ts.transpileModule(fs.readFileSync(filename, 'utf8'), {
      compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, experimentalDecorators: true },
    }).outputText, context, { filename });
    return exports;
  }
  const errors = load('core/http-error.ts');
  const failure = (status, retry = null) => new http.HttpErrorResponse({
    status, error: { message: 'INTERNAL_SECRET', detail: 'STACK_TRACE' },
    headers: new http.HttpHeaders(retry === null ? {} : { 'Retry-After': retry }),
  });
  for (const [value, expected] of [[null, null], ['', null], ['12', 12], ['0', 0], ['garbage', null], ['99999999999999999999', null]])
    assert.equal(errors.retryAfterSeconds(value), expected);
  const now = Date.UTC(2026, 10, 10, 12);
  assert.equal(errors.retryAfterSeconds(new Date(now + 30000).toUTCString(), now), 30);
  assert.equal(errors.retryAfterSeconds(new Date(now - 30000).toUTCString(), now), null);
  for (const status of [0, 400, 401, 403, 404, 409, 429, 500, 503]) {
    const message = errors.httpErrorMessage(failure(status), true);
    assert(!message.includes('INTERNAL_SECRET') && !message.includes('STACK_TRACE'));
  }
  assert.match(errors.httpErrorMessage(failure(429, '12')), /12 segundos/);
  assert.match(errors.httpErrorMessage(failure(429)), /Aguarde/);

  const notifications = [];
  providers.set('NotificationService', { error: message => notifications.push(message), success() {} });
  providers.set('ThemeService', {});
  providers.set('Router', {});
  const { errorInterceptor } = load('core/error.interceptor.ts');
  for (const local of [false, true]) {
    notifications.length = 0;
    const request = new http.HttpRequest('GET', '/isolated', { context: local ? errors.localErrorContext() : new http.HttpContext() });
    let propagated;
    errorInterceptor(request, () => rx.throwError(() => failure(429))).subscribe({ error: error => { propagated = error; } });
    assert.equal(propagated.status, 429);
    assert.equal(notifications.length, local ? 0 : 1, 'One error owner');
  }

  const { LoginComponent } = load('pages/login/login.component.ts');
  for (const [status, expected] of [[401, /E-mail ou senha inválidos/], [429, /Muitas tentativas/], [503, /temporariamente indisponível/], [0, /conexão/], [500, /erro no servidor/]]) {
    let loginCalls = 0;
    const login = new LoginComponent(new forms.FormBuilder(), {
      login: () => { loginCalls++; return rx.throwError(() => failure(status)); },
    }, { navigateByUrl: async () => true });
    login.form.setValue({ email: 'local@example.com', password: 'password' });
    login.submit();
    assert.match(login.error(), expected);
    assert.equal(login.loading(), false);
    assert.equal(loginCalls, 1, 'No automatic retry');
  }
  const loginPending = new rx.Subject();
  let calls = 0;
  const login = new LoginComponent(new forms.FormBuilder(), { login: () => { calls++; return loginPending; } }, {});
  login.form.setValue({ email: 'local@example.com', password: 'password' });
  login.submit(); login.submit();
  assert.equal(calls, 1, 'Ignore duplicate submit while loading');
  loginPending.error(failure(429, '10'));
  assert.equal(login.loading(), false);
  assert.match(login.error(), /10 segundos/);

  // The real auth service marks auth errors as local; this is not just an
  // interceptor test with a manually constructed context.
  let options;
  const { AuthService } = load('core/auth.service.ts');
  const auth = new AuthService({ post: (url, body, opts) => { options = opts; return rx.throwError(() => failure(429)); } });
  auth.login('local@example.com', 'password').subscribe({ error() {} });
  assert.equal(options.context.get(errors.HANDLE_ERROR_LOCALLY), true);
  auth.register('Local', 'local@example.com', 'password').subscribe({ error() {} });
  assert.equal(options.context.get(errors.HANDLE_ERROR_LOCALLY), true);

  const details = { id: 1, name: 'Local', genres: [] };
  const episodes = [{ id: 10, season: 1, number: 1, name: 'Episode' }];
  const mySeries = [{ externalSeriesId: 1, status: 2 }];
  let detailsSource = new rx.Subject(), episodesSource = new rx.Subject(), mineSource = new rx.Subject();
  let watchedSource = new rx.Subject();
  let writes = 0;
  providers.set('EpisodioAssistidoService', {
    obterAssistidosPorSerie: () => watchedSource,
    marcarComoAssistido: () => { writes++; return rx.throwError(() => failure(429)); },
    marcarTemporadaComoAssistida: () => { writes++; return rx.throwError(() => failure(429)); },
  });
  const { SeriesDetailComponent } = load('pages/series-detail/series-detail.component.ts');
  const series = new SeriesDetailComponent({ snapshot: { paramMap: new Map([['id', '1']]) } }, {
    getDetails: () => detailsSource, getEpisodes: () => episodesSource, getMySeries: () => mineSource,
    add: () => { writes++; return rx.throwError(() => failure(429)); },
  });
  series.ngOnInit();
  assert.equal(series.assistidosReady(), false, 'Actions wait for watched data, even when basic reads are partial');
  detailsSource.next(details); detailsSource.complete();
  assert.equal(series.series(), details, 'Successful read available before other reads finish');
  episodesSource.error(failure(429, '9'));
  mineSource.next(mySeries); mineSource.complete();
  assert.equal(series.loading(), false);
  assert.equal(series.savedStatus(), 2);
  assert.equal(series.readErrors().length, 1);
  assert.match(series.readErrors()[0], /9 segundos/);
  watchedSource.error(failure(503));
  assert.equal(series.carregandoAssistidos(), false, 'Error never leaves spinner active');
  assert.equal(series.assistidosReady(), false);
  assert(series.assistidosError());
  watchedSource = rx.of([]);
  series.retryAssistidos();
  assert.equal(series.assistidosError(), null);
  assert.equal(series.assistidosReady(), true);
  assert.equal(series.carregandoAssistidos(), false);
  detailsSource = rx.throwError(() => failure(503));
  episodesSource = rx.of(episodes);
  mineSource = rx.of(mySeries);
  series.load();
  assert.equal(series.series(), details, 'Keep previously obtained details on manual retry failure');
  assert.equal(series.episodes(), episodes);
  assert.equal(series.episodesReady(), true);
  assert.equal(series.loading(), false);
  series.save(2);
  assert.equal(series.savingStatus(), false);
  series.marcarRapidamenteComoAssistido(episodes[0], { stopPropagation() {} });
  assert.equal(series.episodioSalvando(), null);
  series.alternarTemporadaAssistida();
  assert.equal(series.temporadaSalvando(), null);
  assert.equal(writes, 3, 'Failed writes each executed exactly once');

  const profileSource = new rx.Subject(), statsSource = new rx.Subject();
  providers.set('AuthService', { updateUser() {} });
  providers.set('ProfileService', { getProfile: () => profileSource, getStats: () => statsSource });
  const { ProfileComponent } = load('pages/profile/profile.component.ts');
  const profile = new ProfileComponent();
  profile.ngOnInit();
  assert(profile.loadingProfile() && profile.loadingStats());
  profileSource.error(failure(429));
  statsSource.next({ series: 1, episodes: 2, favorites: 0 }); statsSource.complete();
  assert.equal(profile.loadingProfile(), false);
  assert.equal(profile.loadingStats(), false);
  assert(profile.profileError());
  assert.equal(profile.stats().episodes, 2, 'Partial profile success is retained');

  // Lifecycle double used by the real takeUntilDestroyed operator. No timers or
  // network: Subjects allow assertions while operations are still in flight.
  function lifecycle() {
    const callbacks = new Set();
    return {
      destroyed: false,
      onDestroy(callback) { callbacks.add(callback); return () => callbacks.delete(callback); },
      destroy() { this.destroyed = true; for (const callback of [...callbacks]) callback(); },
    };
  }
  const { EpisodioModalComponent } = load('shared/episodio-modal/episodio-modal.component.ts');
  const personagem = { externalCharacterId: 11, nome: 'Local' };
  const registro = { externalEpisodeId: 10, externalCharacterId: 11, sentimentos: ['Feliz'] };
  const votes = [{ externalCharacterId: 11, personagemNome: 'Local', totalVotos: 1, porcentagem: 100 }];
  function modalFixture(initialVote = rx.of([])) {
    const destroy = lifecycle();
    const state = { write: new rx.Subject(), vote: new rx.Subject(), writes: 0, reads: 0, events: [], closeEvents: 0, successes: 0 };
    providers.set('DestroyRef', destroy);
    providers.set('NotificationService', { success: () => state.successes++, error: () => assert.fail('Local errors must not toast') });
    providers.set('EpisodioAssistidoService', {
      obterPersonagens: (seriesId, episodeId, local) => { assert.equal(local, true); return rx.of([personagem]); },
      obterVotacao: (id, local) => { assert.equal(local, true); return ++state.reads === 1 ? initialVote : state.vote; },
      marcarComoAssistido: (id, body, local) => { assert.equal(local, true); state.writes++; return state.write; },
    });
    const component = new EpisodioModalComponent();
    component.episodio.set({ id: 10, season: 1, number: 1, name: 'Local episode' });
    component.seriesId.set(1);
    component.salvo.emit = value => state.events.push(value);
    component.fechar.emit = () => state.closeEvents++;
    component.ngOnInit();
    component.form.setValue({ sentimentos: [component.sentimentos[0].valor], externalCharacterId: 11 });
    assert.equal(component.carregando(), false);
    assert.equal(component.carregandoVotacao(), false);
    return { component, state, destroy };
  }

  // Confirm the write BEFORE the read completes, and acknowledge it exactly once.
  {
    const { component, state } = modalFixture();
    component.salvar(); component.salvar(); component.solicitarFechamento();
    assert.equal(state.writes, 1);
    assert.equal(state.closeEvents, 0, 'Cannot close/cancel a write through modal controls');
    assert.equal(component.salvando(), true);
    state.write.next(registro);
    assert.equal(state.events.length, 1);
    assert.equal(state.events[0], registro, 'Parent event contract is preserved');
    assert.equal(component.salvoComSucesso(), true);
    assert.equal(component.salvando(), false);
    assert.equal(component.carregandoVotacao(), true);
    state.write.next({ ...registro }); // Even a malformed multi-emission source cannot duplicate acknowledgement.
    assert.equal(state.events.length, 1);
    state.vote.next(votes); state.vote.complete();
    assert.equal(component.votacao(), votes);
    assert.equal(component.carregandoVotacao(), false);
    assert.equal(component.erroVotacao(), null);
    assert.equal(state.successes, 1);
    component.salvar();
    assert.equal(state.writes, 1, 'Unchanged confirmed selection cannot be saved again');
    component.solicitarFechamento();
    assert.equal(state.closeEvents, 1);
    // An intentional edit is still supported and emits once for its own confirmed write.
    component.alternarSentimento(component.sentimentos[1].valor);
    state.write = rx.of({ ...registro, sentimentos: ['Feliz', 'Triste'] }); state.vote = rx.of(votes);
    component.salvar();
    assert.equal(state.writes, 2);
    assert.equal(state.events.length, 2);
  }
  for (const status of [429, 503]) {
    const { component, state } = modalFixture();
    component.salvar(); state.write.next(registro); state.vote.error(failure(status, status === 429 ? '8' : null));
    assert.equal(state.events.length, 1);
    assert.equal(component.salvoComSucesso(), true);
    assert.equal(component.mensagemErro(), null, 'Read error is not a write/validation error');
    assert.equal(component.erroVotacao(), errors.httpErrorMessage(failure(status, status === 429 ? '8' : null)));
    assert.equal(component.salvando(), false);
    assert.equal(component.carregandoVotacao(), false);
    assert.equal(component.podeSalvar(), false);
    component.salvar();
    assert.equal(state.writes, 1);
    state.vote = new rx.Subject();
    component.repetirVotacao(); component.repetirVotacao();
    assert.equal(state.reads, 3, 'Initial read + post-write read + one manual retry');
    assert.equal(state.writes, 1, 'Recovering votes only reads');
    assert.equal(component.carregandoVotacao(), true);
    if (status === 503) {
      state.vote.error(failure(429));
      assert.equal(component.carregandoVotacao(), false);
      assert(component.erroVotacao());
      state.vote = rx.of(votes); component.repetirVotacao();
    } else { state.vote.next(votes); state.vote.complete(); }
    assert.equal(component.votacao(), votes);
    assert.equal(component.erroVotacao(), null);
    assert.equal(component.carregandoVotacao(), false);
    assert.equal(state.events.length, 1);
    assert.equal(state.writes, 1);
    component.repetirVotacao();
    assert.equal(state.writes, 1);
  }
  for (const status of [429, 400]) {
    const { component, state } = modalFixture();
    component.salvar(); state.write.error(failure(status));
    assert.equal(state.events.length, 0);
    assert.equal(state.reads, 1, 'Failed write never requests a vote refresh');
    assert.equal(component.salvando(), false);
    assert.equal(component.carregandoVotacao(), false);
    assert.equal(component.salvoComSucesso(), false);
    assert.equal(component.mensagemErro(), errors.httpErrorMessage(failure(status)));
    assert.equal(component.podeSalvar(), true, 'A genuinely failed write may be retried manually');
  }
  {
    const { component, state } = modalFixture();
    component.form.controls.sentimentos.setValue([]);
    component.salvar();
    assert.match(component.mensagemErro(), /Selecione pelo menos/);
    assert.equal(state.writes, 0);
  }
  for (const phase of ['write', 'vote']) {
    const { component, state, destroy } = modalFixture();
    component.salvar();
    if (phase === 'vote') state.write.next(registro);
    destroy.destroy();
    assert.equal(component.salvando(), false);
    assert.equal(component.carregandoVotacao(), false);
    state.write.next(registro); state.vote.next(votes);
    assert.equal(state.events.length, phase === 'write' ? 0 : 1, 'No events after component destruction');
    component.salvar(); component.repetirVotacao();
    assert.equal(state.writes, 1);
  }
  {
    const { component, state, destroy } = modalFixture();
    component.salvo.emit = value => { state.events.push(value); destroy.destroy(); };
    component.salvar(); state.write.next(registro);
    assert.equal(state.events.length, 1);
    assert.equal(state.reads, 1, 'Parent may destroy the modal immediately on confirmation');
    assert.equal(component.salvando(), false);
    assert.equal(component.carregandoVotacao(), false);
  }
  {
    const { component, state } = modalFixture(rx.throwError(() => failure(429)));
    assert.equal(component.personagens().length, 1, 'Initial vote failure does not discard character data');
    assert(component.erroVotacao());
    state.vote = rx.of(votes); component.repetirVotacao();
    assert.equal(state.writes, 0);
    assert.equal(state.events.length, 0);
    assert.equal(component.erroVotacao(), null);
  }
  {
    const { component, state } = modalFixture();
    component.salvo.emit = value => {
      state.events.push(value);
      component.salvar(); component.repetirVotacao(); // Synchronous parent callback cannot reenter the flow.
    };
    component.salvar(); state.write.next(registro); state.vote.next(votes);
    assert.equal(state.events.length, 1);
    assert.equal(state.writes, 1);
    assert.equal(state.reads, 2);
  }

  const { LibraryComponent } = load('pages/library/library.component.ts');
  const libraryItems = [{ id: 'local', externalSeriesId: 1, name: 'Local', status: 2, isAnime: false }];
  function libraryFixture() {
    const destroy = lifecycle();
    providers.set('DestroyRef', destroy);
    const state = { source: new rx.Subject(), reads: 0 };
    const component = new LibraryComponent({ getMySeries: (status, local) => { assert.equal(local, true); state.reads++; return state.source; } });
    component.ngOnInit();
    return { component, state, destroy };
  }
  for (const items of [libraryItems, []]) {
    const { component, state } = libraryFixture();
    assert.equal(component.loading(), true);
    component.load(); component.load(); assert.equal(state.reads, 1);
    state.source.next(items); state.source.complete();
    assert.equal(component.items(), items);
    assert.equal(component.loading(), false);
    assert.equal(component.loaded(), true);
    assert.equal(component.ready(), true);
    assert.equal(component.loadError(), null);
    assert.equal(component.visibleItems().length, items.length);
  }
  for (const status of [429, 503, 0]) {
    const { component, state } = libraryFixture();
    state.source.error(failure(status));
    assert.equal(component.loading(), false);
    assert.equal(component.ready(), false, 'Success/empty sections must be hidden after an error');
    assert.equal(component.loaded(), false);
    assert.equal(component.loadError(), errors.httpErrorMessage(failure(status)));
    state.source = new rx.Subject(); component.load(); component.load();
    assert.equal(state.reads, 2);
    assert.equal(component.loading(), true);
    assert.equal(component.loadError(), null);
    state.source.error(failure(503));
    assert.equal(component.ready(), false);
    assert.equal(component.loading(), false);
    state.source = rx.of(libraryItems); component.load();
    assert.equal(state.reads, 3);
    assert.equal(component.ready(), true);
    assert.equal(component.loadError(), null);
    assert.equal(component.items(), libraryItems);
    state.source = rx.throwError(() => failure(429)); component.load();
    assert.equal(component.items(), libraryItems, 'Keep cached data but do not present it as a successful refresh');
    assert.equal(component.ready(), false);
  }
  {
    const { component, state, destroy } = libraryFixture();
    destroy.destroy();
    assert.equal(component.loading(), false, 'Cancellation finalizes loading');
    assert.equal(component.ready(), false, 'Cancelled initial load is not empty success');
    state.source.next(libraryItems);
    assert.equal(component.items().length, 0);
    component.load(); assert.equal(state.reads, 1);
  }
  // Check the template uses the tested success gate for every collection/empty section.
  const libraryTemplate = fs.readFileSync(path.join(__dirname, '../src/app/pages/library/library.component.html'), 'utf8');
  assert(libraryTemplate.includes('} @else if (loadError(); as message)'));
  assert(libraryTemplate.includes('} @else if (ready() && !visibleItems().length)'));
  assert.equal((libraryTemplate.match(/@if \(ready\(\)\)/g) || []).length, 3);
  assert(!libraryTemplate.includes('@if (!loading())'));

  // Prove the real service methods set the local context, not just the doubles.
  const requestOptions = [];
  providers.set('HttpClient', {
    get: (url, options) => { requestOptions.push(options); return rx.of([]); },
    put: (url, body, options) => { requestOptions.push(options); return rx.of(registro); },
  });
  const { EpisodioAssistidoService } = load('core/episodio-assistido.service.ts');
  const realEpisodes = new EpisodioAssistidoService();
  realEpisodes.obterPersonagens(1, 10, true).subscribe();
  realEpisodes.obterVotacao(10, true).subscribe();
  realEpisodes.marcarComoAssistido(10, {}, true).subscribe();
  for (const options of requestOptions) assert.equal(options.context.get(errors.HANDLE_ERROR_LOCALLY), true);
  realEpisodes.marcarComoAssistido(10, {}).subscribe();
  assert.equal(requestOptions.at(-1).context, undefined, 'Other callers keep their interceptor behavior');
  console.log('PASS modal/library regressions: write acknowledgement once; voting 200/429/503 and read-only recovery; validation; cancellation/reentrancy; library data/empty/error/retry; no concurrent duplicate reads; local error ownership.');
  console.log('PASS rate limiting UI: safe errors; Retry-After seconds/date/missing; one message owner; login 401/429/503/network; partial series reads; manual recovery; loading flags; no write retries; profile partial success.');
})().catch(error => { console.error(error); process.exitCode = 1; });
