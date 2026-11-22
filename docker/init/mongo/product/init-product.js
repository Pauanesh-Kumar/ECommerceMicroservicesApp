db = db.getSiblingDB(process.env.MONGO_PRODUCT_DB);

db.createUser({
    user: process.env.MONGO_PRODUCT_APP_USER,
    pwd: process.env.MONGO_PRODUCT_APP_PASSWORD,
    roles: [{ role: 'readWrite', db: process.env.MONGO_PRODUCT_DB }]
});

db.createCollection('products');
db.products.createIndex({ sku: 1 }, { unique: true });
db.products.createIndex({ category: 1 });
db.products.createIndex({ name: 'text', description: 'text' });

print('mongodb-product: app user + products collection/indexes created');