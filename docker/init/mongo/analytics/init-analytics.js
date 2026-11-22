db = db.getSiblingDB(process.env.MONGO_ANALYTICS_DB);

db.createUser({
    user: process.env.MONGO_ANALYTICS_APP_USER,
    pwd: process.env.MONGO_ANALYTICS_APP_PASSWORD,
    roles: [{ role: 'readWrite', db: process.env.MONGO_ANALYTICS_DB }]
});

db.createCollection('events');
db.events.createIndex({ userId: 1 });
db.events.createIndex({ eventType: 1 });
db.events.createIndex({ timestamp: 1 }, { expireAfterSeconds: 7776000 }); // 90-day TTL

print('mongodb-analytics: app user + events collection/indexes created');