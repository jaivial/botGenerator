-- Migration 003: Add restaurant contact/location columns
-- Adds: contact_phone, contact_email, location, website_url, menu_url to restaurants table

ALTER TABLE restaurants
  ADD COLUMN contact_phone VARCHAR(20) NULL AFTER cif,
  ADD COLUMN contact_email VARCHAR(100) NULL AFTER contact_phone,
  ADD COLUMN location VARCHAR(255) NULL AFTER contact_email,
  ADD COLUMN website_url VARCHAR(255) NULL AFTER location,
  ADD COLUMN menu_url VARCHAR(255) NULL AFTER website_url;

UPDATE restaurants SET
  contact_phone = '+34 638 857 294',
  contact_email = 'reservas@alqueriavillacarmen.com',
  location = 'Carrer Sequia Rascanya 2, Catarroja 46470 Valencia',
  website_url = 'https://alqueriavillacarmen.com',
  menu_url = 'https://alqueriavillacarmen.com/menufindesemana.php'
WHERE slug = 'villacarmen';
