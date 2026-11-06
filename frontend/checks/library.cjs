const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const ts = require('typescript');
const vm = require('node:vm');

const signal = initial => {
  let value = initial;
  const read = () => value;
  read.set = next => { value = next; };
  read.update = update => { value = update(value); };
  return read;
};
const source = fs.readFileSync(path.join(__dirname, '../src/app/pages/library/library.component.ts'), 'utf8');
const context = {
  exports: {},
  require: name => {
    if (name === '@angular/core') return {
      Component: () => value => value,
      HostListener: () => () => {},
      inject: () => ({ success() {} }),
      signal,
      computed: calculate => calculate,
      viewChild: () => () => undefined,
      afterRenderEffect() {},
    };
    if (name.includes('series.models')) return { SeriesStatus: { InList: 1, Watching: 2, Finished: 3 } };
    if (name === 'rxjs') return { finalize: callback => callback };
    return {};
  },
};
vm.runInNewContext(ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, experimentalDecorators: true },
}).outputText, context);
const { LibraryComponent } = context.exports;
const series = { id: 'series', externalSeriesId: 1, name: 'House', status: 1, isFavorite: true, isAnime: false };
const anime = { id: 'anime', externalSeriesId: 2, name: 'Naruto', status: 2, isFavorite: true, isAnime: true };
const unknown = { id: 'unknown', externalSeriesId: 3, name: 'Unknown', status: 3, isFavorite: false, isAnime: null };
const response = value => ({
  subscribe: ({ next }) => next(value),
  pipe: finalize => ({ subscribe: ({ next }) => { next(value); finalize(); } }),
});
const library = new LibraryComponent({
  updateStatus: (id, status) => response({ ...anime, status }),
  updateFavorite: (id, isFavorite) => {
    assert.equal(id, anime.externalSeriesId);
    return response({ ...anime, isFavorite });
  },
  remove: () => response(),
});
const ids = items => Array.from(items, item => item.id);
library.items.set([unknown, anime, series]);
assert.deepEqual(ids(library.visibleItems()), ['series', 'unknown']);
assert.deepEqual(ids(library.visibleAnimeItems()), ['anime']);
assert.deepEqual(ids(library.favoriteItems()), ['series']);
assert.deepEqual(ids(library.favoriteAnimeItems()), ['anime']);
assert.equal(library.counts().all, 3);
assert.equal(library.counts().watching, 1);
library.filter.set(2);
assert.deepEqual(ids(library.visibleItems()), []);
assert.deepEqual(ids(library.visibleAnimeItems()), ['anime']);
assert.deepEqual(ids(library.favoriteItems()), ['series']);
library.alterarStatus(anime, 3);
assert.deepEqual(ids(library.visibleAnimeItems()), []);
assert.deepEqual(ids(library.favoriteAnimeItems()), ['anime']);
const event = { preventDefault() {}, stopPropagation() {} };
library.toggleFavorite(library.animeItems()[0], event);
assert.deepEqual(ids(library.favoriteAnimeItems()), []);
assert.equal(library.animeItems()[0].isFavorite, false);
assert.equal(library.animeItems()[0].status, 3);
assert.deepEqual(ids(library.favoriteItems()), ['series']);
library.toggleFavorite(library.animeItems()[0], event);
assert.deepEqual(ids(library.favoriteAnimeItems()), ['anime']);
assert.equal(library.savingFavorites().size, 0);
library.filter.set(3);
assert.deepEqual(ids(library.visibleAnimeItems()), ['anime']);
assert.equal(library.counts().finished, 2);
library.remove(anime);
assert.deepEqual(ids(library.animeItems()), []);
assert.deepEqual(ids(library.favoriteAnimeItems()), []);
assert.equal(library.counts().all, 2);
library.items.set([]);
assert.deepEqual(ids(library.visibleItems()), []);
assert.deepEqual(ids(library.visibleAnimeItems()), []);
assert.deepEqual(ids(library.favoriteItems()), []);
assert.deepEqual(ids(library.favoriteAnimeItems()), []);
console.log('Library checks passed: separation, favorites, status filters, counts, status updates, removal and empty collection.');
