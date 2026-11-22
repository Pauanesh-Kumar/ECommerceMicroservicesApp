// Runs on first container boot inside the ecommerce database

db = db.getSiblingDB('productservice_db');
db.createCollection('products');
db.products.createIndex({ sku: 1 }, { unique: true });
db.products.createIndex({ category: 1 });
db.products.createIndex({ name: 'text', description: 'text' });

db = db.getSiblingDB('notificationservice_db');
db.createCollection('notifications');
db.notifications.createIndex({ userId: 1 });
db.notifications.createIndex({ createdAt: 1 }, { expireAfterSeconds: 2592000 }); // 30-day TTL

db = db.getSiblingDB('analyticsservice_db');
db.createCollection('events');
db.events.createIndex({ userId: 1 });
db.events.createIndex({ eventType: 1 });
db.events.createIndex({ timestamp: 1 }, { expireAfterSeconds: 7776000 }); // 90-day TTL

print('MongoDB collections and indexes created');
