import { AfterViewInit, Component, DestroyRef, ElementRef, inject, viewChild } from '@angular/core';
import * as maplibregl from 'maplibre-gl';
import type { ErrorEvent } from 'maplibre-gl';

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

  private map: maplibregl.Map | undefined;

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

    this.map.on('load', () => console.debug('[MapComponent] map load event fired'));
    this.map.on('error', (event: ErrorEvent) => console.error('[MapComponent] map error event', event));

    this.destroyRef.onDestroy(() => {
      console.debug('[MapComponent] onDestroy: removing MapLibre map instance');
      this.map?.remove();
      this.map = undefined;
    });
  }
}
