import { AfterViewInit, Component, DestroyRef, ElementRef, inject, viewChild } from '@angular/core';
import * as maplibregl from 'maplibre-gl';
import type { ErrorEvent } from 'maplibre-gl';
import { bboxPolygon, mask } from '@turf/turf';
import type { Feature, MultiPolygon, Polygon } from 'geojson';

@Component({
  selector: 'app-map',
  imports: [],
  templateUrl: './map.html',
  styleUrl: './map.css'
})
export class MapComponent implements AfterViewInit {
  private readonly mapContainer = viewChild.required<ElementRef<HTMLDivElement>>('mapContainer');
  private readonly destroyRef = inject(DestroyRef);

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

  private map: maplibregl.Map | undefined;
  private moldovaBorder: Feature<Polygon | MultiPolygon> | undefined;

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
      void this.loadMoldovaBorder();
    });
    this.map.on('error', (event: ErrorEvent) => console.error('[MapComponent] map error event', event));

    this.destroyRef.onDestroy(() => {
      console.debug('[MapComponent] onDestroy: removing MapLibre map instance');
      this.map?.remove();
      this.map = undefined;
    });
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
}
