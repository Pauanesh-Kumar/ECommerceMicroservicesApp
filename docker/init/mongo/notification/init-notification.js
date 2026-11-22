// Runs once on first container boot for mongodb-notification.
// Creates the app-scoped user (least privilege) + the notifications collection/indexes.

db = db.getSiblingDB(process.env.MONGO_NOTIF_DB);

db.createUser({
    user: process.env.MONGO_NOTIF_APP_USER,
    pwd: process.env.MONGO_NOTIF_APP_PASSWORD,
    roles: [{ role: 'readWrite', db: process.env.MONGO_NOTIF_DB }]
});

db.createCollection('notifications');
db.notifications.createIndex({ userId: 1 });
db.notifications.createIndex({ status: 1 });
db.notifications.createIndex({ eventType: 1 });
db.notifications.createIndex({ createdAt: -1 });
db.notifications.createIndex({ userId: 1, createdAt: -1 });
db.notifications.createIndex({ createdAt: 1 }, { expireAfterSeconds: 2592000 }); // 30-day TTL

print('mongodb-notification: app user + notifications collection/indexes created');