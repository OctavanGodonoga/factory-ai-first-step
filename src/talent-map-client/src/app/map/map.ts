import { AfterViewInit, Component, DestroyRef, ElementRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import * as maplibregl from 'maplibre-gl';
import type { ErrorEvent } from 'maplibre-gl';
import { bboxPolygon, booleanPointInPolygon, mask, point } from '@turf/turf';
import type { Feature, MultiPolygon, Polygon } from 'geojson';
import { MapPointsService } from './map-points.service';
import type { MapPointDetails } from './map-points.service';

@Component({
  selector: 'app-map',
  imports: [],
  templateUrl: './map.html',
  styleUrl: './map.css'
})
export class MapComponent implements AfterViewInit {
  private readonly mapContainer = viewChild.required<ElementRef<HTMLDivElement>>('mapContainer');
  private readonly destroyRef = inject(DestroyRef);
  private readonly mapPointsService = inject(MapPointsService);

  readonly selectedPoint = signal<MapPointDetails | null>(null);

  // OpenFreeMap "liberty" style: free, no API key, explicitly intended for production use.
  // https://openfreemap.org
  private readonly style = 'https://tiles.openfreemap.org/styles/liberty';
  private readonly center: maplibregl.LngLatLike = [28.3699, 47.0105];
  private readonly minZoom = 6;
  private readonly maxZoom = 18;
  private readonly maxBounds: maplibregl.LngLatBoundsLike = [
    [26.0, 45.0],
    [30.5, 49.0]
  ];
  // Margin (degrees) added around maxBounds to build the outside-mask polygon,
  // so the mask stays compact instead of covering the whole world (turf.mask default).
  private readonly maskBoundsMargin = 1;
  // No environment.ts/fileReplacements setup exists yet in this project (angular.json has no
  // configurations for it) — hardcoded here consistent with `style` above. Matches the API's
  // local dev port from launchSettings.json (http profile), paired with the `ng serve` origin
  // (4200) already whitelisted in appsettings.Development.json's Cors:AllowedOrigins.
  private readonly mapPointsApiBaseUrl = 'http://localhost:5205';

  private map: maplibregl.Map | undefined;
  private moldovaBorder: Feature<Polygon | MultiPolygon> | undefined;
  private temporaryMarker: maplibregl.Marker | undefined;
  private selectedMarker: maplibregl.Marker | undefined;

  ngAfterViewInit(): void {
    console.debug('[MapComponent] ngAfterViewInit: initializing MapLibre map');

    this.map = new maplibregl.Map({
      container: this.mapContainer().nativeElement,
      style: this.style,
      center: this.center,
      zoom: this.minZoom,
      minZoom: this.minZoom,
      maxZoom: this.maxZoom,
      maxBounds: this.maxBounds
    });

    this.map.on('load', () => {
      console.debug('[MapComponent] map load event fired');
      void this.loadMoldovaBorder().then(() => this.addMapPointsLayer());
    });
    this.map.on('error', (event: ErrorEvent) => console.error('[MapComponent] map error event', event));
    this.map.on('click', (event: maplibregl.MapMouseEvent) => this.handleMapClick(event));

    this.destroyRef.onDestroy(() => {
      console.debug('[MapComponent] onDestroy: removing MapLibre map instance');
      this.map?.remove();
      this.map = undefined;
      this.temporaryMarker?.remove();
      this.temporaryMarker = undefined;
      this.selectedMarker?.remove();
      this.selectedMarker = undefined;
    });
  }

  private handleMapClick(event: maplibregl.MapMouseEvent): void {
    const hits = this.map?.queryRenderedFeatures(event.point, { layers: ['mappoints-circle'] }) ?? [];

    if (hits.length > 0) {
      const feature = hits[0];
      const id = String(feature.properties?.['id']);
      console.debug('[MapComponent] existing point clicked', { id });
      this.selectExistingPoint(id, feature.geometry);
      return;
    }

    if (this.selectedPoint() !== null) {
      this.selectedMarker?.remove();
      this.selectedMarker = undefined;
      this.selectedPoint.set(null);
    }

    const { lng, lat } = event.lngLat;

    if (!this.moldovaBorder) {
      console.warn('[MapComponent] click ignored: Moldova border not loaded yet');
      return;
    }

    const clickPoint = point([lng, lat]);
    const insideMoldova = booleanPointInPolygon(clickPoint, this.moldovaBorder);
    console.debug('[MapComponent] click point-in-polygon check', { lng, lat, insideMoldova });

    if (!insideMoldova) {
      return;
    }

    this.showTemporaryMarker(event.lngLat);
  }

  private selectExistingPoint(id: string, geometry: GeoJSON.Geometry): void {
    if (!this.map) {
      return;
    }

    const lngLat = (geometry as GeoJSON.Point).coordinates as [number, number];

    this.selectedMarker?.remove();
    this.selectedMarker = new maplibregl.Marker({ color: '#2563eb' }).setLngLat(lngLat).addTo(this.map);

    this.mapPointsService
      .getById(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (details) => {
          console.debug('[MapComponent] point details loaded', { id, details });
          this.selectedPoint.set(details);
        },
        error: (error) => {
          console.error('[MapComponent] failed to load point details', { id, error });
          this.selectedPoint.set(null);
        }
      });
  }

  private showTemporaryMarker(lngLat: maplibregl.LngLatLike): void {
    if (!this.map) {
      return;
    }

    this.temporaryMarker?.remove();
    this.temporaryMarker = new maplibregl.Marker({ color: '#ffcc00' }).setLngLat(lngLat).addTo(this.map);
    console.debug('[MapComponent] temporary marker placed', { lngLat });
  }

  private async loadMoldovaBorder(): Promise<void> {
    console.debug('[MapComponent] loading Moldova border geojson from /data/moldova-border.geojson');

    let borderFeature: Feature<Polygon | MultiPolygon>;
    try {
      const response = await fetch('/data/moldova-border.geojson');
      if (!response.ok) {
        throw new Error(`unexpected response status ${response.status}`);
      }
      borderFeature = (await response.json()) as Feature<Polygon | MultiPolygon>;
    } catch (error) {
      console.error('[MapComponent] failed to load Moldova border geojson', error);
      return;
    }
    console.debug('[MapComponent] Moldova border geojson loaded', { featureCount: 1 });

    this.moldovaBorder = borderFeature;
    if (!this.map) {
      return;
    }

    this.map.addSource('moldova-boundary', { type: 'geojson', data: borderFeature });

    // Mask must be added before the border line so the line renders on top of it.
    this.addMoldovaMaskLayer(borderFeature);

    this.map.addLayer({
      id: 'moldova-border-line',
      type: 'line',
      source: 'moldova-boundary',
      paint: {
        'line-color': '#ffcc00',
        'line-width': 2
      }
    });
    console.debug('[MapComponent] moldova-border-line layer added');
  }

  private addMoldovaMaskLayer(borderFeature: Feature<Polygon | MultiPolygon>): void {
    if (!this.map) {
      return;
    }

    console.debug('[MapComponent] computing outside-Moldova mask polygon');
    try {
      const bounds = this.maxBounds as [[number, number], [number, number]];
      const boundingBbox: [number, number, number, number] = [
        bounds[0][0] - this.maskBoundsMargin,
        bounds[0][1] - this.maskBoundsMargin,
        bounds[1][0] + this.maskBoundsMargin,
        bounds[1][1] + this.maskBoundsMargin
      ];
      const boundingMask = bboxPolygon(boundingBbox);
      const maskFeature = mask(borderFeature, boundingMask);

      this.map.addSource('moldova-mask', { type: 'geojson', data: maskFeature });
      this.map.addLayer({
        id: 'moldova-mask-fill',
        type: 'fill',
        source: 'moldova-mask',
        paint: {
          'fill-color': '#0a0a0a',
          'fill-opacity': 0.55
        }
      });
      console.debug('[MapComponent] moldova-mask-fill layer added');
    } catch (error) {
      console.error('[MapComponent] failed to compute/add Moldova mask layer', error);
    }
  }

  private addMapPointsLayer(): void {
    if (!this.map) {
      return;
    }

    const tilesUrl = `${this.mapPointsApiBaseUrl}/api/map/points/{z}/{x}/{y}.pbf`;
    console.debug('[MapComponent] adding mappoints vector source + layers', { tilesUrl });
    try {
      this.map.addSource('mappoints', {
        type: 'vector',
        tiles: [tilesUrl],
        minzoom: this.minZoom,
        maxzoom: this.maxZoom
      });

      this.map.addLayer({
        id: 'mappoints-circle',
        type: 'circle',
        source: 'mappoints',
        'source-layer': 'mappoints',
        paint: {
          'circle-radius': ['interpolate', ['linear'], ['zoom'], 6, 3, 14, 7, 18, 10],
          'circle-color': [
            'match',
            ['get', 'status'],
            'active',
            '#16a34a',
            'inactive',
            '#9ca3af',
            // fallback for unknown statuses
            '#f59e0b'
          ],
          'circle-stroke-width': 2,
          'circle-stroke-color': [
            'match',
            ['get', 'type'],
            'generic',
            '#ffffff',
            // fallback for unknown types — same white until real types are introduced
            '#ffffff'
          ]
        }
      });

      this.map.addLayer({
        id: 'mappoints-label',
        type: 'symbol',
        source: 'mappoints',
        'source-layer': 'mappoints',
        minzoom: 13,
        layout: {
          'text-field': ['get', 'type'],
          'text-size': 12,
          'text-offset': [0, 1.4],
          'text-anchor': 'top'
        },
        paint: {
          'text-color': '#111827',
          'text-halo-color': '#ffffff',
          'text-halo-width': 1.2
        }
      });

      console.debug('[MapComponent] mappoints source + circle/symbol layers added', { tilesUrl });
    } catch (error) {
      console.error('[MapComponent] failed to add mappoints layer', error);
    }
  }
}
