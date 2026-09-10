// Seed script for scale-testing the map tile clustering/caching feature.
//
// Usage:
//   mongosh mongodb://localhost:27017/TalentMap scripts/seed-mappoints.js
//
// To run the extended 1M-point scale test, change N below to 1000000 and re-run.
// (Re-running does not clear previous points — drop the collection first if you
// want a clean slate: db.mapPoints.deleteMany({}).)

const N = 100000;
const BATCH_SIZE = 1000;

// Matches maxBounds in src/talent-map-client/src/app/map/map.ts
const MIN_LON = 26.0;
const MAX_LON = 30.5;
const MIN_LAT = 45.0;
const MAX_LAT = 49.0;

const TYPES = ["generic", "school", "hospital", "office"];
const STATUSES = ["active", "inactive"];

function randomInRange(min, max) {
  return min + Math.random() * (max - min);
}

function randomFrom(values) {
  return values[Math.floor(Math.random() * values.length)];
}

function buildPoint(i) {
  const lon = randomInRange(MIN_LON, MAX_LON);
  const lat = randomInRange(MIN_LAT, MAX_LAT);

  return {
    name: `Seed Point ${i}`,
    description: null,
    location: {
      type: "Point",
      coordinates: [lon, lat],
    },
    type: randomFrom(TYPES),
    status: randomFrom(STATUSES),
    createdAt: new Date(),
  };
}

const collection = db.getCollection("mapPoints");

let inserted = 0;
while (inserted < N) {
  const batchCount = Math.min(BATCH_SIZE, N - inserted);
  const batch = [];
  for (let i = 0; i < batchCount; i++) {
    batch.push(buildPoint(inserted + i));
  }

  collection.insertMany(batch);
  inserted += batchCount;
  print(`Inserted ${inserted}/${N}`);
}

print(`Done. Inserted ${inserted} map points into TalentMap.mapPoints.`);
