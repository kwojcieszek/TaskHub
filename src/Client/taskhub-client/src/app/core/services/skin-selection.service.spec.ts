import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DEFAULT_SKIN_ID, SkinSelectionService } from './skin-selection.service';

describe('SkinSelectionService', () => {
  const manifestUrl = 'skins/skins.json';
  const manifest = {
    skins: [
      { file: 'forest.css', name: 'Forest', version: '2' },
      { file: '../outside.css', name: 'Outside' },
      { file: 'https://example.com/x.css', name: 'Remote' }
    ]
  };

  let service: SkinSelectionService;
  let http: HttpTestingController;

  function linkElement(): HTMLLinkElement | null {
    return document.getElementById('taskhub-skin') as HTMLLinkElement | null;
  }

  beforeEach(() => {
    localStorage.removeItem('taskhub.skin');
    linkElement()?.remove();

    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(SkinSelectionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.removeItem('taskhub.skin');
    linkElement()?.remove();
  });

  it('starts with the default skin', () => {
    expect(service.selectedId()).toBe(DEFAULT_SKIN_ID);
    expect(linkElement()).toBeNull();
  });

  it('offers only the skins whose file is a plain stylesheet name in the skins directory', () => {
    service.refresh();

    http.expectOne(manifestUrl).flush(manifest);

    expect(service.skins().map((skin) => skin.id)).toEqual(['forest']);
  });

  it('links and remembers the stylesheet of a chosen skin', () => {
    service.select('forest');

    http.expectOne(manifestUrl).flush(manifest);

    expect(service.selectedId()).toBe('forest');
    expect(linkElement()?.getAttribute('href')).toBe('skins/forest.css?v=2');
    expect(document.head.lastElementChild).toBe(linkElement());
    expect(localStorage.getItem('taskhub.skin')).toContain('"id":"forest"');
  });

  it('falls back to the default skin when the chosen one is no longer deployed', () => {
    service.select('removed');

    http.expectOne(manifestUrl).flush(manifest);

    expect(service.selectedId()).toBe(DEFAULT_SKIN_ID);
    expect(linkElement()).toBeNull();
  });

  it('offers no skins when the skins directory has no manifest', () => {
    service.refresh();

    http.expectOne(manifestUrl).flush('Not found', { status: 404, statusText: 'Not Found' });

    expect(service.skins()).toEqual([]);
  });

  it('removes the stylesheet when the default skin is chosen', () => {
    service.select('forest');
    http.expectOne(manifestUrl).flush(manifest);

    service.select(DEFAULT_SKIN_ID);

    expect(linkElement()).toBeNull();
    expect(localStorage.getItem('taskhub.skin')).toBeNull();
  });
});
