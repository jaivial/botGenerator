-- Phase 4: durable booking-confirmation outbox.
-- Apply manually to the bot MySQL database before deploying code that enables this worker.
-- The application never executes this DDL or creates this table at runtime.

CREATE TABLE booking_confirmation_outbox (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    booking_id BIGINT UNSIGNED NOT NULL,
    notification_type VARCHAR(64) NOT NULL,
    provider VARCHAR(32) NOT NULL,
    phone_number VARCHAR(32) NOT NULL,
    payload_json JSON NOT NULL,
    state VARCHAR(16) NOT NULL DEFAULT 'pending',
    attempts INT UNSIGNED NOT NULL DEFAULT 0,
    next_attempt_at DATETIME(6) NULL,
    last_error TEXT NULL,
    claim_token CHAR(36) NULL,
    lease_expires_at DATETIME(6) NULL,
    accepted_at DATETIME(6) NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_booking_confirmation_outbox (booking_id, notification_type),
    KEY ix_booking_confirmation_outbox_due (state, next_attempt_at, id),
    KEY ix_booking_confirmation_outbox_lease (state, lease_expires_at),
    CONSTRAINT chk_booking_confirmation_outbox_state
        CHECK (state IN ('pending', 'processing', 'accepted', 'failed'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='Durable provider-submission outbox for booking confirmations';

-- State semantics:
-- pending: never attempted; processing: leased by a worker; accepted: provider accepted submission;
-- failed: last submission failed. A failed row retries only while attempts is below configured max
-- and next_attempt_at is non-NULL. The worker reclaims an expired processing lease after restart.
