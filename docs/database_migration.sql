-- =============================================================================
-- Database Migration: Conversation Messages Table
-- =============================================================================
-- This migration creates the table for storing conversation history
-- to support external booking initialization and persistent chat context.
-- =============================================================================

-- Create the conversation_messages table
CREATE TABLE IF NOT EXISTS conversation_messages (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    phone_number VARCHAR(20) NOT NULL COMMENT 'Normalized phone number (digits only)',
    role VARCHAR(20) NOT NULL COMMENT 'Message role: user, assistant, or system',
    content TEXT NOT NULL COMMENT 'Message content',
    timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Message timestamp',
    message_id VARCHAR(100) NULL COMMENT 'External message ID if available',
    from_name VARCHAR(100) NULL COMMENT 'Display name of the sender',
    
    -- Indexes for efficient querying
    INDEX idx_phone_number (phone_number),
    INDEX idx_timestamp (timestamp),
    INDEX idx_phone_timestamp (phone_number, timestamp)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Stores conversation history for WhatsApp bot interactions';

-- =============================================================================
-- Optional: Create a view for recent conversations
-- =============================================================================
CREATE OR REPLACE VIEW recent_conversations AS
SELECT 
    phone_number,
    COUNT(*) as message_count,
    MAX(timestamp) as last_activity,
    TIMESTAMPDIFF(MINUTE, MAX(timestamp), NOW()) as minutes_since_last
FROM conversation_messages
GROUP BY phone_number
ORDER BY last_activity DESC;

-- =============================================================================
-- Sample queries for common operations
-- =============================================================================

-- Get conversation history for a phone number
-- SELECT * FROM conversation_messages 
-- WHERE phone_number = '34612345678' 
-- ORDER BY timestamp ASC 
-- LIMIT 100;

-- Check if a phone number has existing messages
-- SELECT EXISTS(
--     SELECT 1 FROM conversation_messages 
--     WHERE phone_number = '34612345678'
-- ) as has_messages;

-- Clear conversation history for a phone number
-- DELETE FROM conversation_messages WHERE phone_number = '34612345678';

-- Get conversation statistics
-- SELECT 
--     COUNT(DISTINCT phone_number) as unique_users,
--     COUNT(*) as total_messages,
--     AVG(message_count) as avg_messages_per_user
-- FROM (
--     SELECT phone_number, COUNT(*) as message_count
--     FROM conversation_messages
--     GROUP BY phone_number
-- ) stats;
