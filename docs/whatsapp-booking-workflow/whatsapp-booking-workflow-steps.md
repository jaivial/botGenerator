# whatsapp-booking-workflow - Development Steps

## Task Overview
**Original Request**: Create 300 steps to verify the correct workflow for the WhatsApp booking system, create necessary PHP files, integrate with uazapi, and deploy to production.

**Total Steps**: 300
**Steps Directory**: `./docs/whatsapp-booking-workflow/steps/`

**Key Requirements**:
- Booking data: date, party size, time, arroz type, arroz servings, baby strollers, high chairs
- Date validation via check_date_availability.php
- Rice type validation via get_available_rice_types.php
- UAZAPI integration for /send/text and /send/menu endpoints
- Phone number formatting (remove +34, 9 characters)
- Booking insertion via insert_booking.php
- Notifications to: 34686969914, 34638857294, 34692747052
- Deployment via deploy.sh

---

## SECTION 1: RESEARCH & DISCOVERY (Steps 1-30)

---

## Step 1: Read and analyze conectaVILLACARMEN.php database connection
### Objective
Understand the database connection setup and available connection objects ($conn, $pdo)

### Requirements
- [ ] Read conectaVILLACARMEN.php file
- [ ] Document connection objects exported
- [ ] Note environment variable handling

### Acceptance Criteria
- Connection method documented
- Available variables ($conn, $pdo) understood

### Dependencies
- Prior steps: None
- Files needed: /home/jaime/Documents/projects/alqueriavillacarmen/conectaVILLACARMEN.php

---

## Step 2: Analyze check_date_availability.php API structure
### Objective
Understand how date availability checking works

### Requirements
- [ ] Read check_date_availability.php
- [ ] Document input parameters (newDate, partySize, currentTime, bookingId)
- [ ] Document all response codes and messages

### Acceptance Criteria
- All API parameters documented
- Response structure understood

### Dependencies
- Prior steps: Step 1
- Files needed: /home/jaime/Documents/projects/alqueriavillacarmen/api/check_date_availability.php

---

## Step 3: Analyze get_available_rice_types.php API structure
### Objective
Understand how rice type retrieval works

### Requirements
- [ ] Read get_available_rice_types.php
- [ ] Document the database query
- [ ] Document response format

### Acceptance Criteria
- API response format documented
- Database table (FINDE) structure understood

### Dependencies
- Prior steps: Step 1
- Files needed: /home/jaime/Documents/projects/alqueriavillacarmen/api/get_available_rice_types.php

---

## Step 4: Analyze insert_booking.php API structure
### Objective
Understand booking insertion process

### Requirements
- [ ] Read insert_booking.php
- [ ] Document all required parameters
- [ ] Document WhatsApp notification integration

### Acceptance Criteria
- All booking fields documented
- Transaction handling understood

### Dependencies
- Prior steps: Step 1
- Files needed: /home/jaime/Documents/projects/alqueriavillacarmen/insert_booking.php

---

## Step 5: Analyze send_whatsapp_uazapi.php integration
### Objective
Understand UAZAPI integration patterns

### Requirements
- [ ] Read send_whatsapp_uazapi.php
- [ ] Document sendUazApiWhatsApp function
- [ ] Document sendWhatsAppConfirmationWithButtonsUazApi function

### Acceptance Criteria
- UAZAPI endpoint usage documented
- Payload formats understood

### Dependencies
- Prior steps: None
- Files needed: /home/jaime/Documents/projects/alqueriavillacarmen/includes/send_whatsapp_uazapi.php

---

## Step 6: Document UAZAPI /send/text endpoint
### Objective
Document the text message sending endpoint

### Requirements
- [ ] Review existing implementation
- [ ] Document endpoint URL format
- [ ] Document payload structure

### Acceptance Criteria
- Endpoint: {UAZAPI_URL}/send/text?token={TOKEN}
- Payload: { number, text }

### Dependencies
- Prior steps: Step 5
- Files needed: send_whatsapp_uazapi.php

---

## Step 7: Document UAZAPI /send/menu endpoint
### Objective
Document the menu/button message endpoint

### Requirements
- [ ] Review existing implementation
- [ ] Document payload for button type
- [ ] Document choices format

### Acceptance Criteria
- Endpoint: {UAZAPI_URL}/send/menu?token={TOKEN}
- Payload: { number, type, text, choices }

### Dependencies
- Prior steps: Step 5
- Files needed: send_whatsapp_uazapi.php

---

## Step 8: Analyze BotGenerator MainConversationAgent
### Objective
Understand how the C# bot processes messages

### Requirements
- [ ] Read MainConversationAgent.cs
- [ ] Document processing flow
- [ ] Document response parsing

### Acceptance Criteria
- Message flow documented
- Intent extraction understood

### Dependencies
- Prior steps: None
- Files needed: /home/jaime/Documents/projects/botGenerator/src/BotGenerator.Core/Agents/MainConversationAgent.cs

---

## Step 9: Analyze BookingHandler in BotGenerator
### Objective
Understand how bookings are created from C# side

### Requirements
- [ ] Read BookingHandler.cs
- [ ] Document CreateBookingAsync method
- [ ] Identify integration points with PHP APIs

### Acceptance Criteria
- Booking creation flow documented
- API integration points identified

### Dependencies
- Prior steps: Step 8
- Files needed: /home/jaime/Documents/projects/botGenerator/src/BotGenerator.Core/Handlers/BookingHandler.cs

---

## Step 10: Analyze WhatsAppService in BotGenerator
### Objective
Understand C# UAZAPI integration

### Requirements
- [ ] Read WhatsAppService.cs
- [ ] Document SendTextAsync method
- [ ] Document SendMenuAsync method

### Acceptance Criteria
- UAZAPI integration patterns documented
- Authentication method understood

### Dependencies
- Prior steps: Step 8
- Files needed: /home/jaime/Documents/projects/botGenerator/src/BotGenerator.Core/Services/WhatsAppService.cs

---

## Step 11: Document database schema for bookings table
### Objective
Understand booking data structure

### Requirements
- [ ] Check villacarmen_schema.sql
- [ ] Document all columns in bookings table
- [ ] Note data types and constraints

### Acceptance Criteria
- Schema documented
- Required vs optional fields identified

### Dependencies
- Prior steps: Step 1
- Files needed: villacarmen_schema.sql

---

## Step 12: Document FINDE table schema for rice types
### Objective
Understand rice menu data structure

### Requirements
- [ ] Analyze FINDE table query in get_available_rice_types.php
- [ ] Document TIPO and DESCRIPCION columns
- [ ] Document active flag filtering

### Acceptance Criteria
- Rice data structure understood
- Query logic documented

### Dependencies
- Prior steps: Step 3
- Files needed: get_available_rice_types.php

---

## Step 13: Analyze closed_days and opened_days logic
### Objective
Understand day status determination

### Requirements
- [ ] Read fetch_closed_days.php
- [ ] Document closed_days table structure
- [ ] Document opened_days table structure

### Acceptance Criteria
- Day status logic documented
- Override mechanism understood

### Dependencies
- Prior steps: Step 2
- Files needed: fetch_closed_days.php

---

## Step 14: Document special holidays handling
### Objective
Understand special holiday restrictions

### Requirements
- [ ] Review special holidays in check_date_availability.php
- [ ] Document dates: 12-24, 12-25, 12-31, 01-01, 01-05, 01-06
- [ ] Document special messages

### Acceptance Criteria
- Holiday dates documented
- Rejection messages documented

### Dependencies
- Prior steps: Step 2
- Files needed: check_date_availability.php

---

## Step 15: Analyze 35-day booking range limit
### Objective
Understand future booking restrictions

### Requirements
- [ ] Review isDateTooFarInFuture function
- [ ] Document calculation logic
- [ ] Document user messaging

### Acceptance Criteria
- 35-day limit understood
- Error message documented

### Dependencies
- Prior steps: Step 2
- Files needed: check_date_availability.php

---

## Step 16: Document hourly availability checking
### Objective
Understand time slot availability

### Requirements
- [ ] Analyze gethourdata.php call
- [ ] Document available_hours response
- [ ] Document time formatting

### Acceptance Criteria
- Hour availability logic documented
- Time slot structure understood

### Dependencies
- Prior steps: Step 2
- Files needed: check_date_availability.php, api/gethourdata.php

---

## Step 17: Analyze daily capacity limits
### Objective
Understand party size restrictions

### Requirements
- [ ] Review daily_limits table query
- [ ] Document default limit (100)
- [ ] Document capacity calculation

### Acceptance Criteria
- Capacity logic documented
- Override mechanism understood

### Dependencies
- Prior steps: Step 2
- Files needed: check_date_availability.php

---

## Step 18: Document phone number formatting requirements
### Objective
Understand phone normalization rules

### Requirements
- [ ] Review existing phone handling
- [ ] Document +34 removal
- [ ] Document 9-character requirement

### Acceptance Criteria
- Formatting rules documented
- Edge cases identified

### Dependencies
- Prior steps: Step 4
- Files needed: insert_booking.php

---

## Step 19: Analyze notification recipient list
### Objective
Document notification phone numbers

### Requirements
- [ ] Document 34686969914
- [ ] Document 34638857294
- [ ] Document 34692747052

### Acceptance Criteria
- All recipient numbers documented
- Notification purpose understood

### Dependencies
- Prior steps: Step 5
- Files needed: send_whatsapp_uazapi.php

---

## Step 20: Review deploy.sh script
### Objective
Understand deployment process

### Requirements
- [ ] Read deploy.sh
- [ ] Document git operations
- [ ] Document SSH deployment to server

### Acceptance Criteria
- Deployment flow documented
- Server connection understood

### Dependencies
- Prior steps: None
- Files needed: /home/jaime/Documents/projects/alqueriavillacarmen/deploy.sh

---

## Step 21: Document environment variables
### Objective
List all required environment variables

### Requirements
- [ ] Review .env file
- [ ] Document UAZAPI_URL
- [ ] Document UAZAPI_TOKEN

### Acceptance Criteria
- All env vars documented
- Configuration understood

### Dependencies
- Prior steps: Step 5
- Files needed: .env

---

## Step 22: Analyze conversation state tracking
### Objective
Understand how booking state is maintained

### Requirements
- [ ] Review ConversationState model
- [ ] Document state fields
- [ ] Document state extraction

### Acceptance Criteria
- State model documented
- Tracking mechanism understood

### Dependencies
- Prior steps: Step 8
- Files needed: ConversationState.cs

---

## Step 23: Document BookingData model structure
### Objective
Understand booking data transfer object

### Requirements
- [ ] Review BookingData.cs
- [ ] Document all properties
- [ ] Document optional fields

### Acceptance Criteria
- All fields documented
- Nullable fields identified

### Dependencies
- Prior steps: Step 8
- Files needed: BookingData.cs

---

## Step 24: Analyze IntentType enumeration
### Objective
Understand message intent classification

### Requirements
- [ ] Review IntentType.cs
- [ ] Document Booking intent
- [ ] Document other intents

### Acceptance Criteria
- All intents documented
- Usage context understood

### Dependencies
- Prior steps: Step 8
- Files needed: IntentType.cs

---

## Step 25: Document BOOKING_REQUEST command format
### Objective
Understand booking command parsing

### Requirements
- [ ] Review ParseAiResponse in MainConversationAgent
- [ ] Document BOOKING_REQUEST|format
- [ ] Document field extraction

### Acceptance Criteria
- Command format documented
- Parsing regex understood

### Dependencies
- Prior steps: Step 8
- Files needed: MainConversationAgent.cs

---

## Step 26: Analyze rice extraction from AI response
### Objective
Understand rice type parsing

### Requirements
- [ ] Review ExtractAdditionalBookingFields
- [ ] Document arroz regex
- [ ] Document servings extraction

### Acceptance Criteria
- Rice extraction logic documented
- Regex patterns understood

### Dependencies
- Prior steps: Step 8
- Files needed: MainConversationAgent.cs

---

## Step 27: Document high chairs and strollers extraction
### Objective
Understand baby equipment parsing

### Requirements
- [ ] Review tronas regex
- [ ] Review carritos regex
- [ ] Document default values

### Acceptance Criteria
- Equipment extraction documented
- Default (0) behavior understood

### Dependencies
- Prior steps: Step 8
- Files needed: MainConversationAgent.cs

---

## Step 28: Analyze system prompt template
### Objective
Understand AI prompt structure

### Requirements
- [ ] Read system-main.txt
- [ ] Document template variables
- [ ] Document validation rules

### Acceptance Criteria
- Prompt structure documented
- Variable placeholders identified

### Dependencies
- Prior steps: Step 8
- Files needed: system-main.txt

---

## Step 29: Document booking flow prompt
### Objective
Understand booking conversation guidance

### Requirements
- [ ] Read booking-flow.txt
- [ ] Document step sequence
- [ ] Document validation rules

### Acceptance Criteria
- Booking flow documented
- Required questions identified

### Dependencies
- Prior steps: Step 28
- Files needed: booking-flow.txt

---

## Step 30: Create research summary document
### Objective
Compile all research findings

### Requirements
- [ ] Create summary of all API endpoints
- [ ] Document data flow diagram
- [ ] List all integration points

### Acceptance Criteria
- Summary document created
- All APIs and their purposes listed

### Dependencies
- Prior steps: Steps 1-29
- Files needed: All previous research outputs

---

## SECTION 2: DATE AVAILABILITY VALIDATION (Steps 31-80)

---

## Step 31: Create test case - Valid future date (simple check)
### Objective
Test simple date availability check

### Requirements
- [ ] Test with only newDate parameter
- [ ] Verify success response
- [ ] Document expected behavior

### Acceptance Criteria
- API returns available=true for valid date
- simpleCheck=true in response

### Dependencies
- Prior steps: Step 2
- Files needed: check_date_availability.php

---

## Step 32: Create test case - Monday (normally closed)
### Objective
Test Monday closure

### Requirements
- [ ] Test Monday date
- [ ] Verify closed_day response
- [ ] Check message content

### Acceptance Criteria
- API returns available=false
- reason='closed_day'

### Dependencies
- Prior steps: Step 31
- Files needed: check_date_availability.php

---

## Step 33: Create test case - Tuesday (normally closed)
### Objective
Test Tuesday closure

### Requirements
- [ ] Test Tuesday date
- [ ] Verify closed_day response

### Acceptance Criteria
- API returns available=false
- reason='closed_day'

### Dependencies
- Prior steps: Step 31
- Files needed: check_date_availability.php

---

## Step 34: Create test case - Wednesday (normally closed)
### Objective
Test Wednesday closure

### Requirements
- [ ] Test Wednesday date
- [ ] Verify closed_day response

### Acceptance Criteria
- API returns available=false
- reason='closed_day'

### Dependencies
- Prior steps: Step 31
- Files needed: check_date_availability.php

---

## Step 35: Create test case - Thursday (normally open)
### Objective
Test Thursday availability

### Requirements
- [ ] Test Thursday date
- [ ] Verify available response

### Acceptance Criteria
- API returns available=true
- date_open reason

### Dependencies
- Prior steps: Step 31
- Files needed: check_date_availability.php

---

## Step 36: Create test case - Friday (normally open)
### Objective
Test Friday availability

### Requirements
- [ ] Test Friday date
- [ ] Verify available response

### Acceptance Criteria
- API returns available=true

### Dependencies
- Prior steps: Step 31
- Files needed: check_date_availability.php

---

## Step 37: Create test case - Saturday (normally open)
### Objective
Test Saturday availability

### Requirements
- [ ] Test Saturday date
- [ ] Verify available response

### Acceptance Criteria
- API returns available=true

### Dependencies
- Prior steps: Step 31
- Files needed: check_date_availability.php

---

## Step 38: Create test case - Sunday (normally open)
### Objective
Test Sunday availability

### Requirements
- [ ] Test Sunday date
- [ ] Verify available response

### Acceptance Criteria
- API returns available=true

### Dependencies
- Prior steps: Step 31
- Files needed: check_date_availability.php

---

## Step 39: Create test case - December 24 (special holiday)
### Objective
Test Christmas Eve restriction

### Requirements
- [ ] Test 12-24 date
- [ ] Verify special_holiday response

### Acceptance Criteria
- API returns available=false
- reason='special_holiday'

### Dependencies
- Prior steps: Step 14
- Files needed: check_date_availability.php

---

## Step 40: Create test case - December 25 (special holiday)
### Objective
Test Christmas Day restriction

### Requirements
- [ ] Test 12-25 date
- [ ] Verify special_holiday response

### Acceptance Criteria
- API returns available=false
- reason='special_holiday'

### Dependencies
- Prior steps: Step 14
- Files needed: check_date_availability.php

---

## Step 41: Create test case - December 31 (special holiday)
### Objective
Test New Year's Eve restriction

### Requirements
- [ ] Test 12-31 date
- [ ] Verify special_holiday response

### Acceptance Criteria
- API returns available=false
- reason='special_holiday'

### Dependencies
- Prior steps: Step 14
- Files needed: check_date_availability.php

---

## Step 42: Create test case - January 1 (special holiday)
### Objective
Test New Year's Day restriction

### Requirements
- [ ] Test 01-01 date
- [ ] Verify special_holiday response

### Acceptance Criteria
- API returns available=false
- reason='special_holiday'

### Dependencies
- Prior steps: Step 14
- Files needed: check_date_availability.php

---

## Step 43: Create test case - January 5 (special holiday)
### Objective
Test Three Kings Eve restriction

### Requirements
- [ ] Test 01-05 date
- [ ] Verify special_holiday response

### Acceptance Criteria
- API returns available=false
- reason='special_holiday'

### Dependencies
- Prior steps: Step 14
- Files needed: check_date_availability.php

---

## Step 44: Create test case - January 6 (special holiday)
### Objective
Test Three Kings Day restriction

### Requirements
- [ ] Test 01-06 date
- [ ] Verify special_holiday response

### Acceptance Criteria
- API returns available=false
- reason='special_holiday'

### Dependencies
- Prior steps: Step 14
- Files needed: check_date_availability.php

---

## Step 45: Create test case - Date more than 35 days away
### Objective
Test future date limit

### Requirements
- [ ] Calculate date 40 days from today
- [ ] Verify too_far_future response

### Acceptance Criteria
- API returns available=false
- reason='too_far_future'

### Dependencies
- Prior steps: Step 15
- Files needed: check_date_availability.php

---

## Step 46: Create test case - Date exactly 35 days away
### Objective
Test boundary condition

### Requirements
- [ ] Calculate date exactly 35 days from today
- [ ] Verify available response

### Acceptance Criteria
- API returns available=true
- Within acceptable range

### Dependencies
- Prior steps: Step 15
- Files needed: check_date_availability.php

---

## Step 47: Create test case - Date 36 days away
### Objective
Test boundary +1 condition

### Requirements
- [ ] Calculate date 36 days from today
- [ ] Verify too_far_future response

### Acceptance Criteria
- API returns available=false
- reason='too_far_future'

### Dependencies
- Prior steps: Step 15
- Files needed: check_date_availability.php

---

## Step 48: Create test case - Full check with party size 2
### Objective
Test hourly availability for small group

### Requirements
- [ ] Test with newDate and partySize=2
- [ ] Verify hourly availability returned

### Acceptance Criteria
- API returns availableHours array
- hasAvailability calculated

### Dependencies
- Prior steps: Step 16
- Files needed: check_date_availability.php

---

## Step 49: Create test case - Full check with party size 10
### Objective
Test hourly availability for medium group

### Requirements
- [ ] Test with partySize=10
- [ ] Verify capacity check

### Acceptance Criteria
- API returns appropriate availability
- Capacity considered

### Dependencies
- Prior steps: Step 16
- Files needed: check_date_availability.php

---

## Step 50: Create test case - Full check with party size 20
### Objective
Test hourly availability for large group

### Requirements
- [ ] Test with partySize=20
- [ ] Verify capacity check

### Acceptance Criteria
- API handles large groups correctly

### Dependencies
- Prior steps: Step 16
- Files needed: check_date_availability.php

---

## Step 51: Create test case - Full check with specific time available
### Objective
Test time slot availability

### Requirements
- [ ] Test with currentTime=14:00
- [ ] Verify currentTimeAvailable response

### Acceptance Criteria
- currentTimeAvailable=true when slot is free

### Dependencies
- Prior steps: Step 16
- Files needed: check_date_availability.php

---

## Step 52: Create test case - Full check with specific time unavailable
### Objective
Test time slot unavailability

### Requirements
- [ ] Test with a busy time slot
- [ ] Verify alternative hours offered

### Acceptance Criteria
- currentTimeAvailable=false
- availableHours contains alternatives

### Dependencies
- Prior steps: Step 16
- Files needed: check_date_availability.php

---

## Step 53: Create test case - Explicitly opened Monday
### Objective
Test opened_days override

### Requirements
- [ ] Add Monday to opened_days
- [ ] Test availability

### Acceptance Criteria
- API returns available=true
- Override works correctly

### Dependencies
- Prior steps: Step 13
- Files needed: check_date_availability.php

---

## Step 54: Create test case - Explicitly closed Saturday
### Objective
Test closed_days override

### Requirements
- [ ] Add Saturday to closed_days
- [ ] Test availability

### Acceptance Criteria
- API returns available=false
- Override works correctly

### Dependencies
- Prior steps: Step 13
- Files needed: check_date_availability.php

---

## Step 55: Create test case - Capacity exceeded for date
### Objective
Test daily limit enforcement

### Requirements
- [ ] Set daily_limit for date
- [ ] Request partySize exceeding limit

### Acceptance Criteria
- API returns available=false
- reason='capacity_exceeded_new_date'

### Dependencies
- Prior steps: Step 17
- Files needed: check_date_availability.php

---

## Step 56: Create test case - No hours available
### Objective
Test full day scenario

### Requirements
- [ ] Setup date with no available hours
- [ ] Verify no_hours_available response

### Acceptance Criteria
- API returns available=false
- reason='no_hours_available'

### Dependencies
- Prior steps: Step 16
- Files needed: check_date_availability.php

---

## Step 57: Create test case - Invalid date format
### Objective
Test error handling for bad input

### Requirements
- [ ] Send malformed date
- [ ] Verify error response

### Acceptance Criteria
- API handles gracefully
- Appropriate error message

### Dependencies
- Prior steps: Step 31
- Files needed: check_date_availability.php

---

## Step 58: Create test case - Missing newDate parameter
### Objective
Test required parameter validation

### Requirements
- [ ] Send request without newDate
- [ ] Verify error response

### Acceptance Criteria
- success=false
- 'Missing required parameter' message

### Dependencies
- Prior steps: Step 31
- Files needed: check_date_availability.php

---

## Step 59: Create test case - Booking modification with existing ID
### Objective
Test modification availability check

### Requirements
- [ ] Include bookingId in request
- [ ] Verify capacity calculation excludes current booking

### Acceptance Criteria
- Correct capacity calculation
- Current booking excluded

### Dependencies
- Prior steps: Step 17
- Files needed: check_date_availability.php

---

## Step 60: Create test case - Past date rejection
### Objective
Test past date handling

### Requirements
- [ ] Send yesterday's date
- [ ] Verify rejection

### Acceptance Criteria
- API rejects past dates
- Appropriate message

### Dependencies
- Prior steps: Step 31
- Files needed: check_date_availability.php

---

## Step 61: Create PHP test script for date validation
### Objective
Create automated test script

### Requirements
- [ ] Create api/tests/test_date_availability.php
- [ ] Include all test cases
- [ ] Output results as JSON

### Acceptance Criteria
- Script runs all tests
- Results clearly formatted

### Dependencies
- Prior steps: Steps 31-60
- Files needed: New file

---

## Step 62: Document date validation response formats
### Objective
Create API response documentation

### Requirements
- [ ] Document all response fields
- [ ] Document all reason codes
- [ ] Document message templates

### Acceptance Criteria
- Complete response documentation

### Dependencies
- Prior steps: Steps 31-60
- Files needed: None

---

## Step 63: Verify date validation handles timezone correctly
### Objective
Test timezone handling

### Requirements
- [ ] Test with different timezone inputs
- [ ] Verify consistent behavior

### Acceptance Criteria
- Timezone handling documented
- Edge cases covered

### Dependencies
- Prior steps: Step 31
- Files needed: check_date_availability.php

---

## Step 64: Test date validation API response time
### Objective
Measure performance

### Requirements
- [ ] Time multiple requests
- [ ] Document average response time

### Acceptance Criteria
- Response time < 500ms
- Performance acceptable

### Dependencies
- Prior steps: Step 61
- Files needed: test_date_availability.php

---

## Step 65: Test concurrent date validation requests
### Objective
Test concurrent access

### Requirements
- [ ] Send multiple simultaneous requests
- [ ] Verify consistent responses

### Acceptance Criteria
- No race conditions
- Consistent results

### Dependencies
- Prior steps: Step 61
- Files needed: test_date_availability.php

---

## Step 66: Create integration test - Date to AI response
### Objective
Test C# integration with date API

### Requirements
- [ ] Call check_date_availability from C#
- [ ] Parse response correctly

### Acceptance Criteria
- Integration works
- Response parsing correct

### Dependencies
- Prior steps: Step 61
- Files needed: C# test project

---

## Step 67: Test date API with WhatsApp date formats
### Objective
Test user input variations

### Requirements
- [ ] Test "el sábado" style inputs
- [ ] Test "15 de diciembre" style

### Acceptance Criteria
- Various formats handled
- Parser integration documented

### Dependencies
- Prior steps: Step 31
- Files needed: check_date_availability.php

---

## Step 68: Verify date API error logging
### Objective
Test error logging

### Requirements
- [ ] Trigger error conditions
- [ ] Verify logs captured

### Acceptance Criteria
- Errors logged correctly
- Debug info available

### Dependencies
- Prior steps: Step 57
- Files needed: check_date_availability.php

---

## Step 69: Test date API CORS headers
### Objective
Verify cross-origin support

### Requirements
- [ ] Test from different origin
- [ ] Verify CORS headers

### Acceptance Criteria
- CORS headers present
- Cross-origin works

### Dependencies
- Prior steps: Step 31
- Files needed: check_date_availability.php

---

## Step 70: Document date validation workflow diagram
### Objective
Create visual documentation

### Requirements
- [ ] Create flowchart of date validation
- [ ] Document decision points
- [ ] Document API calls

### Acceptance Criteria
- Workflow diagram created
- All paths documented

### Dependencies
- Prior steps: Steps 31-60
- Files needed: None

---

## Step 71: Create AI agent prompt for date unavailable scenarios
### Objective
Create prompt for unavailable dates

### Requirements
- [ ] Draft prompt template
- [ ] Include suggestion logic
- [ ] Include UAZAPI message trigger

### Acceptance Criteria
- Prompt handles unavailable dates
- Suggests alternatives

### Dependencies
- Prior steps: Step 70
- Files needed: New prompt file

---

## Step 72: Test AI agent date unavailable response
### Objective
Verify AI response for closed days

### Requirements
- [ ] Test with closed day
- [ ] Verify UAZAPI message sent

### Acceptance Criteria
- AI suggests alternatives
- WhatsApp message sent

### Dependencies
- Prior steps: Step 71
- Files needed: Prompt files

---

## Step 73: Create AI agent prompt for date available scenarios
### Objective
Create prompt for available dates

### Requirements
- [ ] Draft continuation prompt
- [ ] Include next question logic

### Acceptance Criteria
- Conversation continues smoothly
- Time question asked

### Dependencies
- Prior steps: Step 71
- Files needed: New prompt file

---

## Step 74: Test date + time validation combined
### Objective
Test two-step validation

### Requirements
- [ ] First call with just date
- [ ] Second call with date + time

### Acceptance Criteria
- Both validations work
- Flow is seamless

### Dependencies
- Prior steps: Step 51
- Files needed: check_date_availability.php

---

## Step 75: Create error handling for date API failures
### Objective
Handle API errors gracefully

### Requirements
- [ ] Create fallback behavior
- [ ] Log API failures
- [ ] Notify user appropriately

### Acceptance Criteria
- Graceful degradation
- User informed

### Dependencies
- Prior steps: Step 68
- Files needed: Error handling code

---

## Step 76: Test date validation with bookingId for modifications
### Objective
Test modification scenario

### Requirements
- [ ] Create existing booking
- [ ] Test modification availability

### Acceptance Criteria
- Correct capacity calculation
- Existing booking excluded

### Dependencies
- Prior steps: Step 59
- Files needed: check_date_availability.php

---

## Step 77: Document date validation API in OpenAPI format
### Objective
Create formal API documentation

### Requirements
- [ ] Create OpenAPI spec
- [ ] Document all endpoints
- [ ] Include examples

### Acceptance Criteria
- Complete API spec
- Usable documentation

### Dependencies
- Prior steps: Step 62
- Files needed: New OpenAPI file

---

## Step 78: Create unit tests for date helper functions
### Objective
Test helper functions

### Requirements
- [ ] Test isNormallyClosedDay
- [ ] Test isDateClosed
- [ ] Test isDateTooFarInFuture

### Acceptance Criteria
- All helpers tested
- Edge cases covered

### Dependencies
- Prior steps: Step 61
- Files needed: Test file

---

## Step 79: Verify date API works after deployment
### Objective
Post-deployment verification

### Requirements
- [ ] Test on production URL
- [ ] Verify all test cases pass

### Acceptance Criteria
- Production API works
- All tests pass

### Dependencies
- Prior steps: Step 61
- Files needed: None (uses production)

---

## Step 80: Create date validation summary report
### Objective
Compile date validation findings

### Requirements
- [ ] Document all test results
- [ ] List any issues found
- [ ] Provide recommendations

### Acceptance Criteria
- Complete summary
- All findings documented

### Dependencies
- Prior steps: Steps 31-79
- Files needed: Previous outputs

---

## SECTION 3: RICE TYPE VALIDATION (Steps 81-130)

---

## Step 81: Test get_available_rice_types API response
### Objective
Verify rice types retrieval

### Requirements
- [ ] Call API
- [ ] Verify response format

### Acceptance Criteria
- riceTypes array returned
- count field accurate

### Dependencies
- Prior steps: Step 3
- Files needed: get_available_rice_types.php

---

## Step 82: Test rice API when no active types
### Objective
Test empty result handling

### Requirements
- [ ] Deactivate all rice types
- [ ] Verify empty response

### Acceptance Criteria
- riceTypes: []
- count: 0

### Dependencies
- Prior steps: Step 81
- Files needed: get_available_rice_types.php

---

## Step 83: Test rice API with multiple active types
### Objective
Verify multiple results

### Requirements
- [ ] Ensure multiple active types
- [ ] Verify all returned

### Acceptance Criteria
- All active types returned
- Correct count

### Dependencies
- Prior steps: Step 81
- Files needed: get_available_rice_types.php

---

## Step 84: Create rice type comparison function
### Objective
Create fuzzy matching for rice types

### Requirements
- [ ] Create PHP function for comparison
- [ ] Handle variations (case, accents)
- [ ] Return match confidence

### Acceptance Criteria
- Function created
- Variations handled

### Dependencies
- Prior steps: Step 81
- Files needed: New PHP file

---

## Step 85: Test rice comparison - Exact match
### Objective
Test exact string match

### Requirements
- [ ] Compare identical strings
- [ ] Verify match found

### Acceptance Criteria
- Match returned
- Confidence 100%

### Dependencies
- Prior steps: Step 84
- Files needed: Rice comparison function

---

## Step 86: Test rice comparison - Case insensitive
### Objective
Test case variations

### Requirements
- [ ] Compare "PAELLA" vs "paella"
- [ ] Verify match found

### Acceptance Criteria
- Match found
- Case handled

### Dependencies
- Prior steps: Step 84
- Files needed: Rice comparison function

---

## Step 87: Test rice comparison - Accent handling
### Objective
Test accent variations

### Requirements
- [ ] Compare "señoret" vs "senoret"
- [ ] Verify match found

### Acceptance Criteria
- Match found
- Accents handled

### Dependencies
- Prior steps: Step 84
- Files needed: Rice comparison function

---

## Step 88: Test rice comparison - Partial match
### Objective
Test substring matching

### Requirements
- [ ] Compare "paella" against full names
- [ ] Find best match

### Acceptance Criteria
- Best match found
- Partial matching works

### Dependencies
- Prior steps: Step 84
- Files needed: Rice comparison function

---

## Step 89: Test rice comparison - No match found
### Objective
Test non-existent rice type

### Requirements
- [ ] Search for "pizza"
- [ ] Verify no match

### Acceptance Criteria
- No match found
- Appropriate response

### Dependencies
- Prior steps: Step 84
- Files needed: Rice comparison function

---

## Step 90: Create rice validation API endpoint
### Objective
Create validation endpoint

### Requirements
- [ ] Create api/validate_rice_type.php
- [ ] Accept user input
- [ ] Return match or suggestions

### Acceptance Criteria
- Endpoint created
- Validation works

### Dependencies
- Prior steps: Steps 84-89
- Files needed: New PHP file

---

## Step 91: Test rice validation - Valid type
### Objective
Test with valid rice type

### Requirements
- [ ] Send valid rice name
- [ ] Verify success response

### Acceptance Criteria
- valid=true
- matchedType returned

### Dependencies
- Prior steps: Step 90
- Files needed: validate_rice_type.php

---

## Step 92: Test rice validation - Invalid type
### Objective
Test with invalid rice type

### Requirements
- [ ] Send invalid rice name
- [ ] Verify failure response

### Acceptance Criteria
- valid=false
- suggestions offered

### Dependencies
- Prior steps: Step 90
- Files needed: validate_rice_type.php

---

## Step 93: Create UAZAPI menu message for rice selection
### Objective
Create rice menu message

### Requirements
- [ ] Format rice types as menu
- [ ] Include prices
- [ ] Add menu button URL

### Acceptance Criteria
- Menu message formatted
- URL included

### Dependencies
- Prior steps: Step 7
- Files needed: send_whatsapp_uazapi.php

---

## Step 94: Test UAZAPI /send/menu for rice suggestion
### Objective
Test menu message sending

### Requirements
- [ ] Send rice menu message
- [ ] Verify delivery

### Acceptance Criteria
- Message delivered
- Menu button works

### Dependencies
- Prior steps: Step 93
- Files needed: send_whatsapp_uazapi.php

---

## Step 95: Create function to send rice menu on invalid type
### Objective
Automate rice menu sending

### Requirements
- [ ] Create sendRiceMenuSuggestion function
- [ ] Include button to menu URL
- [ ] Format error message

### Acceptance Criteria
- Function created
- Menu sent on invalid rice

### Dependencies
- Prior steps: Steps 93-94
- Files needed: send_whatsapp_uazapi.php

---

## Step 96: Test rice menu URL (menufindesemana.php)
### Objective
Verify menu URL works

### Requirements
- [ ] Test URL access
- [ ] Verify rice types displayed

### Acceptance Criteria
- URL accessible
- Rice menu displayed

### Dependencies
- Prior steps: Step 93
- Files needed: menufindesemana.php

---

## Step 97: Test AI rice validation integration
### Objective
Test RiceValidatorAgent with API

### Requirements
- [ ] Call RiceValidatorAgent
- [ ] Verify API integration

### Acceptance Criteria
- Agent uses API
- Validation works

### Dependencies
- Prior steps: Step 90
- Files needed: RiceValidatorAgent.cs

---

## Step 98: Test rice validation - User says "no quiero arroz"
### Objective
Handle rice refusal

### Requirements
- [ ] Parse "no quiero arroz"
- [ ] Set arroz_type to null

### Acceptance Criteria
- Rice fields set to null
- No further rice questions

### Dependencies
- Prior steps: Step 97
- Files needed: RiceValidatorAgent.cs

---

## Step 99: Test rice validation - User says "sin arroz"
### Objective
Handle rice refusal variant

### Requirements
- [ ] Parse "sin arroz"
- [ ] Set arroz_type to null

### Acceptance Criteria
- Rice fields set to null
- Conversation continues

### Dependencies
- Prior steps: Step 98
- Files needed: RiceValidatorAgent.cs

---

## Step 100: Test rice validation - User says rice type directly
### Objective
Handle direct rice type

### Requirements
- [ ] Parse "paella valenciana"
- [ ] Validate and accept

### Acceptance Criteria
- Rice type validated
- Servings question asked

### Dependencies
- Prior steps: Step 91
- Files needed: RiceValidatorAgent.cs

---

## Step 101: Test rice validation - User says partial type
### Objective
Handle partial rice type

### Requirements
- [ ] Parse "paella" only
- [ ] Find best match or ask

### Acceptance Criteria
- Best match found or
- Clarification requested

### Dependencies
- Prior steps: Step 88
- Files needed: RiceValidatorAgent.cs

---

## Step 102: Test rice servings extraction
### Objective
Extract servings count

### Requirements
- [ ] Parse "4 raciones"
- [ ] Extract number

### Acceptance Criteria
- arroz_servings=4
- Correctly parsed

### Dependencies
- Prior steps: Step 26
- Files needed: MainConversationAgent.cs

---

## Step 103: Test rice servings - Various formats
### Objective
Handle servings variations

### Requirements
- [ ] Test "4 raciones", "cuatro", "para 4"
- [ ] Extract correctly

### Acceptance Criteria
- All formats work
- Number extracted

### Dependencies
- Prior steps: Step 102
- Files needed: MainConversationAgent.cs

---

## Step 104: Create test for rice + servings in one message
### Objective
Handle combined input

### Requirements
- [ ] Parse "paella para 4"
- [ ] Extract both values

### Acceptance Criteria
- Rice type and servings extracted
- Both values correct

### Dependencies
- Prior steps: Steps 100, 102
- Files needed: MainConversationAgent.cs

---

## Step 105: Test rice validation flow end-to-end
### Objective
Full rice flow test

### Requirements
- [ ] Start conversation
- [ ] Go through rice flow
- [ ] Verify final state

### Acceptance Criteria
- Complete flow works
- State updated correctly

### Dependencies
- Prior steps: Steps 81-104
- Files needed: All rice-related files

---

## Step 106: Document rice validation API
### Objective
Create API documentation

### Requirements
- [ ] Document validate_rice_type.php
- [ ] Document get_available_rice_types.php
- [ ] Include examples

### Acceptance Criteria
- Complete documentation
- Clear examples

### Dependencies
- Prior steps: Steps 81-105
- Files needed: None

---

## Step 107: Create rice validation error handling
### Objective
Handle API failures

### Requirements
- [ ] Handle database errors
- [ ] Provide fallback behavior

### Acceptance Criteria
- Graceful failure
- User informed

### Dependencies
- Prior steps: Step 90
- Files needed: validate_rice_type.php

---

## Step 108: Test rice API with special characters
### Objective
Handle special input

### Requirements
- [ ] Test with emojis
- [ ] Test with symbols

### Acceptance Criteria
- Input sanitized
- No errors

### Dependencies
- Prior steps: Step 90
- Files needed: validate_rice_type.php

---

## Step 109: Test rice validation performance
### Objective
Measure response time

### Requirements
- [ ] Time API calls
- [ ] Document performance

### Acceptance Criteria
- Response < 300ms
- Acceptable performance

### Dependencies
- Prior steps: Step 90
- Files needed: validate_rice_type.php

---

## Step 110: Create AI prompt for invalid rice response
### Objective
Craft AI response for invalid rice

### Requirements
- [ ] Include menu suggestion
- [ ] Be helpful and friendly

### Acceptance Criteria
- Natural response
- Menu URL included

### Dependencies
- Prior steps: Step 95
- Files needed: Prompt files

---

## Step 111: Test rice menu button format for UAZAPI
### Objective
Verify button format

### Requirements
- [ ] Format: "Ver Carta|URL"
- [ ] Test delivery

### Acceptance Criteria
- Button displays
- URL works

### Dependencies
- Prior steps: Step 93
- Files needed: send_whatsapp_uazapi.php

---

## Step 112: Test rice validation with database update
### Objective
Verify live data

### Requirements
- [ ] Add new rice type
- [ ] Verify API returns it

### Acceptance Criteria
- New type appears
- Real-time update

### Dependencies
- Prior steps: Step 81
- Files needed: Database

---

## Step 113: Test rice validation with inactive type
### Objective
Test filtering

### Requirements
- [ ] Deactivate rice type
- [ ] Verify not returned

### Acceptance Criteria
- Inactive excluded
- Only active returned

### Dependencies
- Prior steps: Step 112
- Files needed: Database

---

## Step 114: Create rice validation unit tests
### Objective
PHP unit tests

### Requirements
- [ ] Test comparison function
- [ ] Test API endpoint

### Acceptance Criteria
- All tests pass
- Coverage complete

### Dependencies
- Prior steps: Step 90
- Files needed: Test file

---

## Step 115: Test rice flow with conversation history
### Objective
Verify state persistence

### Requirements
- [ ] Set rice in previous message
- [ ] Verify not asked again

### Acceptance Criteria
- Rice not re-asked
- State remembered

### Dependencies
- Prior steps: Step 105
- Files needed: ConversationHistoryService.cs

---

## Step 116: Document rice types database structure
### Objective
Document FINDE table

### Requirements
- [ ] Document columns
- [ ] Document TIPO='ARROZ' filter

### Acceptance Criteria
- Schema documented
- Query documented

### Dependencies
- Prior steps: Step 12
- Files needed: Database schema

---

## Step 117: Test rice validation after deployment
### Objective
Production verification

### Requirements
- [ ] Test on production
- [ ] Verify all cases

### Acceptance Criteria
- Production works
- All tests pass

### Dependencies
- Prior steps: Step 114
- Files needed: Production URL

---

## Step 118: Create rice validation logging
### Objective
Add logging

### Requirements
- [ ] Log validation attempts
- [ ] Log matches/failures

### Acceptance Criteria
- Logs created
- Debug info available

### Dependencies
- Prior steps: Step 90
- Files needed: validate_rice_type.php

---

## Step 119: Test rice validation concurrent access
### Objective
Test concurrency

### Requirements
- [ ] Multiple simultaneous requests
- [ ] Verify consistency

### Acceptance Criteria
- No race conditions
- Consistent results

### Dependencies
- Prior steps: Step 114
- Files needed: Test script

---

## Step 120: Create rice validation summary
### Objective
Compile findings

### Requirements
- [ ] Document all test results
- [ ] List issues found
- [ ] Recommendations

### Acceptance Criteria
- Complete summary
- All findings listed

### Dependencies
- Prior steps: Steps 81-119
- Files needed: Previous outputs

---

## Step 121: Test rice type with price extraction
### Objective
Handle price in type name

### Requirements
- [ ] Parse "Arroz meloso de pulpo (+5€)"
- [ ] Handle price notation

### Acceptance Criteria
- Type matched
- Price handled

### Dependencies
- Prior steps: Step 91
- Files needed: validate_rice_type.php

---

## Step 122: Test rice validation internationalization
### Objective
Handle language variations

### Requirements
- [ ] Test Spanish variations
- [ ] Handle regional terms

### Acceptance Criteria
- Variations handled
- Matching works

### Dependencies
- Prior steps: Step 84
- Files needed: Rice comparison function

---

## Step 123: Create rice validation cache
### Objective
Improve performance

### Requirements
- [ ] Cache rice types
- [ ] Invalidate on update

### Acceptance Criteria
- Caching works
- Performance improved

### Dependencies
- Prior steps: Step 109
- Files needed: validate_rice_type.php

---

## Step 124: Test rice validation with empty database
### Objective
Handle edge case

### Requirements
- [ ] Empty FINDE table
- [ ] Verify behavior

### Acceptance Criteria
- Graceful handling
- User informed

### Dependencies
- Prior steps: Step 82
- Files needed: validate_rice_type.php

---

## Step 125: Document rice validation workflow
### Objective
Create flowchart

### Requirements
- [ ] Document decision tree
- [ ] Include all branches

### Acceptance Criteria
- Flowchart created
- All paths shown

### Dependencies
- Prior steps: Step 120
- Files needed: None

---

## Step 126: Test rice validation with HTTP errors
### Objective
Handle network issues

### Requirements
- [ ] Simulate network failure
- [ ] Verify fallback

### Acceptance Criteria
- Graceful degradation
- User notified

### Dependencies
- Prior steps: Step 107
- Files needed: validate_rice_type.php

---

## Step 127: Create rice validation metrics
### Objective
Track usage

### Requirements
- [ ] Count validations
- [ ] Track success rate

### Acceptance Criteria
- Metrics logged
- Analytics available

### Dependencies
- Prior steps: Step 118
- Files needed: validate_rice_type.php

---

## Step 128: Test rice validation security
### Objective
Verify security

### Requirements
- [ ] Test SQL injection
- [ ] Test XSS

### Acceptance Criteria
- Input sanitized
- No vulnerabilities

### Dependencies
- Prior steps: Step 90
- Files needed: validate_rice_type.php

---

## Step 129: Create rice validation rate limiting
### Objective
Prevent abuse

### Requirements
- [ ] Add rate limiting
- [ ] Handle excessive requests

### Acceptance Criteria
- Rate limiting works
- Abuse prevented

### Dependencies
- Prior steps: Step 119
- Files needed: validate_rice_type.php

---

## Step 130: Final rice validation verification
### Objective
Complete verification

### Requirements
- [ ] Run all tests
- [ ] Verify all scenarios
- [ ] Document results

### Acceptance Criteria
- All tests pass
- Documentation complete

### Dependencies
- Prior steps: Steps 81-129
- Files needed: All rice files

---

## SECTION 4: BOOKING INSERTION (Steps 131-180)

---

## Step 131: Analyze insert_booking.php parameters
### Objective
Document all required fields

### Requirements
- [ ] List all POST parameters
- [ ] Document required vs optional
- [ ] Note data types

### Acceptance Criteria
- All parameters documented
- Types specified

### Dependencies
- Prior steps: Step 4
- Files needed: insert_booking.php

---

## Step 132: Test booking insertion - Minimum required fields
### Objective
Test basic booking

### Requirements
- [ ] Send date, party_size, time, nombre, phone
- [ ] Verify insertion

### Acceptance Criteria
- Booking created
- ID returned

### Dependencies
- Prior steps: Step 131
- Files needed: insert_booking.php

---

## Step 133: Test booking insertion - All fields
### Objective
Test complete booking

### Requirements
- [ ] Include all fields
- [ ] Verify all stored

### Acceptance Criteria
- All fields stored
- Data correct

### Dependencies
- Prior steps: Step 132
- Files needed: insert_booking.php

---

## Step 134: Test booking insertion - With rice
### Objective
Test rice booking

### Requirements
- [ ] Include arroz_type and servings
- [ ] Verify storage

### Acceptance Criteria
- Rice data stored
- Correct values

### Dependencies
- Prior steps: Step 133
- Files needed: insert_booking.php

---

## Step 135: Test booking insertion - Without rice
### Objective
Test no-rice booking

### Requirements
- [ ] Set toggleArroz=false
- [ ] Verify null values

### Acceptance Criteria
- arroz_type=null
- arroz_servings=null

### Dependencies
- Prior steps: Step 133
- Files needed: insert_booking.php

---

## Step 136: Test booking insertion - With baby equipment
### Objective
Test equipment fields

### Requirements
- [ ] Set baby_strollers=2
- [ ] Set high_chairs=1

### Acceptance Criteria
- Equipment stored
- Correct values

### Dependencies
- Prior steps: Step 133
- Files needed: insert_booking.php

---

## Step 137: Test booking insertion - Phone formatting with +34
### Objective
Test phone normalization

### Requirements
- [ ] Send phone with +34
- [ ] Verify stored format

### Acceptance Criteria
- +34 removed
- 9 digits stored

### Dependencies
- Prior steps: Step 18
- Files needed: insert_booking.php

---

## Step 138: Test booking insertion - Phone formatting without code
### Objective
Test direct phone

### Requirements
- [ ] Send 9-digit phone
- [ ] Verify stored correctly

### Acceptance Criteria
- Phone stored as-is
- Correct format

### Dependencies
- Prior steps: Step 137
- Files needed: insert_booking.php

---

## Step 139: Test booking insertion - Special characters in name
### Objective
Test name sanitization

### Requirements
- [ ] Include accents, ñ
- [ ] Verify storage

### Acceptance Criteria
- Characters preserved
- UTF-8 handled

### Dependencies
- Prior steps: Step 132
- Files needed: insert_booking.php

---

## Step 140: Test booking insertion - Long commentary
### Objective
Test commentary field

### Requirements
- [ ] Send long comment
- [ ] Verify storage

### Acceptance Criteria
- Commentary stored
- No truncation

### Dependencies
- Prior steps: Step 133
- Files needed: insert_booking.php

---

## Step 141: Verify booking triggers WhatsApp notification
### Objective
Test notification integration

### Requirements
- [ ] Create booking
- [ ] Verify WhatsApp sent

### Acceptance Criteria
- WhatsApp triggered
- Customer notified

### Dependencies
- Prior steps: Step 132
- Files needed: insert_booking.php

---

## Step 142: Test booking ID generation
### Objective
Verify ID assignment

### Requirements
- [ ] Create multiple bookings
- [ ] Verify unique IDs

### Acceptance Criteria
- IDs auto-increment
- All unique

### Dependencies
- Prior steps: Step 132
- Files needed: insert_booking.php

---

## Step 143: Test booking insertion transaction rollback
### Objective
Test error handling

### Requirements
- [ ] Force error after insert
- [ ] Verify rollback

### Acceptance Criteria
- Transaction rolled back
- No partial data

### Dependencies
- Prior steps: Step 132
- Files needed: insert_booking.php

---

## Step 144: Create booking insertion from C# handler
### Objective
Integrate C# booking

### Requirements
- [ ] Update BookingHandler
- [ ] Call PHP API

### Acceptance Criteria
- C# calls API
- Booking created

### Dependencies
- Prior steps: Step 9
- Files needed: BookingHandler.cs

---

## Step 145: Test C# booking handler error handling
### Objective
Handle API failures

### Requirements
- [ ] Simulate API failure
- [ ] Verify error handling

### Acceptance Criteria
- Error caught
- User informed

### Dependencies
- Prior steps: Step 144
- Files needed: BookingHandler.cs

---

## Step 146: Create phone formatting function
### Objective
Normalize phone numbers

### Requirements
- [ ] Create formatPhoneNumber function
- [ ] Remove +34 prefix
- [ ] Ensure 9 characters

### Acceptance Criteria
- Function created
- All formats handled

### Dependencies
- Prior steps: Step 137
- Files needed: New PHP function

---

## Step 147: Test phone formatting - +34 prefix
### Objective
Test +34 removal

### Requirements
- [ ] Input: +34612345678
- [ ] Output: 612345678

### Acceptance Criteria
- +34 removed
- 9 digits output

### Dependencies
- Prior steps: Step 146
- Files needed: Phone formatting function

---

## Step 148: Test phone formatting - 34 prefix (no plus)
### Objective
Test 34 removal

### Requirements
- [ ] Input: 34612345678
- [ ] Output: 612345678

### Acceptance Criteria
- 34 removed
- 9 digits output

### Dependencies
- Prior steps: Step 146
- Files needed: Phone formatting function

---

## Step 149: Test phone formatting - Already 9 digits
### Objective
Test no change needed

### Requirements
- [ ] Input: 612345678
- [ ] Output: 612345678

### Acceptance Criteria
- No change
- Correct output

### Dependencies
- Prior steps: Step 146
- Files needed: Phone formatting function

---

## Step 150: Test phone formatting - With spaces
### Objective
Test space removal

### Requirements
- [ ] Input: 612 345 678
- [ ] Output: 612345678

### Acceptance Criteria
- Spaces removed
- Correct output

### Dependencies
- Prior steps: Step 146
- Files needed: Phone formatting function

---

## Step 151: Test phone formatting - With dashes
### Objective
Test dash removal

### Requirements
- [ ] Input: 612-345-678
- [ ] Output: 612345678

### Acceptance Criteria
- Dashes removed
- Correct output

### Dependencies
- Prior steps: Step 146
- Files needed: Phone formatting function

---

## Step 152: Test phone formatting - Invalid length
### Objective
Handle invalid input

### Requirements
- [ ] Input: 12345
- [ ] Verify error handling

### Acceptance Criteria
- Error raised
- Invalid detected

### Dependencies
- Prior steps: Step 146
- Files needed: Phone formatting function

---

## Step 153: Create booking notification to restaurant
### Objective
Notify restaurant owners

### Requirements
- [ ] Send to 34686969914
- [ ] Send to 34638857294
- [ ] Send to 34692747052

### Acceptance Criteria
- All 3 receive message
- Booking info included

### Dependencies
- Prior steps: Step 19
- Files needed: send_whatsapp_uazapi.php

---

## Step 154: Test restaurant notification format
### Objective
Verify message format

### Requirements
- [ ] Format: "Reserva hecha por asistente de IA:"
- [ ] Include all booking details

### Acceptance Criteria
- Correct format
- All data included

### Dependencies
- Prior steps: Step 153
- Files needed: send_whatsapp_uazapi.php

---

## Step 155: Test restaurant notification - First number
### Objective
Verify first recipient

### Requirements
- [ ] Send to 34686969914
- [ ] Verify delivery

### Acceptance Criteria
- Message delivered
- Content correct

### Dependencies
- Prior steps: Step 153
- Files needed: send_whatsapp_uazapi.php

---

## Step 156: Test restaurant notification - Second number
### Objective
Verify second recipient

### Requirements
- [ ] Send to 34638857294
- [ ] Verify delivery

### Acceptance Criteria
- Message delivered
- Content correct

### Dependencies
- Prior steps: Step 153
- Files needed: send_whatsapp_uazapi.php

---

## Step 157: Test restaurant notification - Third number
### Objective
Verify third recipient

### Requirements
- [ ] Send to 34692747052
- [ ] Verify delivery

### Acceptance Criteria
- Message delivered
- Content correct

### Dependencies
- Prior steps: Step 153
- Files needed: send_whatsapp_uazapi.php

---

## Step 158: Create customer greeting message
### Objective
Send booking confirmation

### Requirements
- [ ] Create greeting message
- [ ] Include booking summary

### Acceptance Criteria
- Message created
- Summary included

### Dependencies
- Prior steps: Step 141
- Files needed: send_whatsapp_uazapi.php

---

## Step 159: Test customer greeting delivery
### Objective
Verify customer receives message

### Requirements
- [ ] Send greeting
- [ ] Verify delivery

### Acceptance Criteria
- Message delivered
- Customer notified

### Dependencies
- Prior steps: Step 158
- Files needed: send_whatsapp_uazapi.php

---

## Step 160: Test booking with all notifications
### Objective
End-to-end notification test

### Requirements
- [ ] Create booking
- [ ] Verify all 4 messages sent

### Acceptance Criteria
- Customer notified
- All 3 owners notified

### Dependencies
- Prior steps: Steps 153-159
- Files needed: All notification code

---

## Step 161: Create booking insertion API endpoint
### Objective
Create dedicated API

### Requirements
- [ ] Create api/create_booking.php
- [ ] Accept JSON input
- [ ] Return structured response

### Acceptance Criteria
- API created
- JSON in/out

### Dependencies
- Prior steps: Step 131
- Files needed: New PHP file

---

## Step 162: Test new booking API - JSON input
### Objective
Test JSON handling

### Requirements
- [ ] Send JSON body
- [ ] Verify processing

### Acceptance Criteria
- JSON parsed
- Booking created

### Dependencies
- Prior steps: Step 161
- Files needed: api/create_booking.php

---

## Step 163: Test new booking API - Response format
### Objective
Verify response structure

### Requirements
- [ ] Check success field
- [ ] Check booking_id field

### Acceptance Criteria
- success=true
- booking_id returned

### Dependencies
- Prior steps: Step 161
- Files needed: api/create_booking.php

---

## Step 164: Test new booking API - Error response
### Objective
Test error handling

### Requirements
- [ ] Send invalid data
- [ ] Verify error response

### Acceptance Criteria
- success=false
- error message included

### Dependencies
- Prior steps: Step 161
- Files needed: api/create_booking.php

---

## Step 165: Create booking validation before insertion
### Objective
Validate all fields

### Requirements
- [ ] Validate date format
- [ ] Validate time format
- [ ] Validate party_size > 0

### Acceptance Criteria
- Validation works
- Invalid rejected

### Dependencies
- Prior steps: Step 161
- Files needed: api/create_booking.php

---

## Step 166: Test booking validation - Invalid date
### Objective
Reject bad date

### Requirements
- [ ] Send invalid date
- [ ] Verify rejection

### Acceptance Criteria
- Booking rejected
- Error message clear

### Dependencies
- Prior steps: Step 165
- Files needed: api/create_booking.php

---

## Step 167: Test booking validation - Invalid time
### Objective
Reject bad time

### Requirements
- [ ] Send invalid time
- [ ] Verify rejection

### Acceptance Criteria
- Booking rejected
- Error message clear

### Dependencies
- Prior steps: Step 165
- Files needed: api/create_booking.php

---

## Step 168: Test booking validation - Invalid party size
### Objective
Reject bad party size

### Requirements
- [ ] Send party_size=0 or negative
- [ ] Verify rejection

### Acceptance Criteria
- Booking rejected
- Error message clear

### Dependencies
- Prior steps: Step 165
- Files needed: api/create_booking.php

---

## Step 169: Test booking validation - Missing required field
### Objective
Reject incomplete data

### Requirements
- [ ] Omit required field
- [ ] Verify rejection

### Acceptance Criteria
- Booking rejected
- Missing field identified

### Dependencies
- Prior steps: Step 165
- Files needed: api/create_booking.php

---

## Step 170: Document booking API
### Objective
Create API documentation

### Requirements
- [ ] Document all endpoints
- [ ] Document parameters
- [ ] Include examples

### Acceptance Criteria
- Complete documentation
- Clear examples

### Dependencies
- Prior steps: Step 161
- Files needed: None

---

## Step 171: Test booking insertion performance
### Objective
Measure response time

### Requirements
- [ ] Time API calls
- [ ] Document performance

### Acceptance Criteria
- Response < 1s
- Acceptable performance

### Dependencies
- Prior steps: Step 161
- Files needed: api/create_booking.php

---

## Step 172: Test booking insertion concurrent access
### Objective
Test concurrency

### Requirements
- [ ] Multiple simultaneous bookings
- [ ] Verify all created

### Acceptance Criteria
- All bookings created
- No conflicts

### Dependencies
- Prior steps: Step 171
- Files needed: api/create_booking.php

---

## Step 173: Create booking insertion logging
### Objective
Add logging

### Requirements
- [ ] Log all insertions
- [ ] Log errors

### Acceptance Criteria
- Logs created
- Debug info available

### Dependencies
- Prior steps: Step 161
- Files needed: api/create_booking.php

---

## Step 174: Test booking insertion security
### Objective
Verify security

### Requirements
- [ ] Test SQL injection
- [ ] Test XSS

### Acceptance Criteria
- Input sanitized
- No vulnerabilities

### Dependencies
- Prior steps: Step 161
- Files needed: api/create_booking.php

---

## Step 175: Create booking insertion rate limiting
### Objective
Prevent abuse

### Requirements
- [ ] Add rate limiting
- [ ] Handle excessive requests

### Acceptance Criteria
- Rate limiting works
- Abuse prevented

### Dependencies
- Prior steps: Step 172
- Files needed: api/create_booking.php

---

## Step 176: Test booking with duplicate prevention
### Objective
Prevent duplicates

### Requirements
- [ ] Create same booking twice
- [ ] Verify handling

### Acceptance Criteria
- Duplicate handled
- User informed

### Dependencies
- Prior steps: Step 161
- Files needed: api/create_booking.php

---

## Step 177: Create booking confirmation email
### Objective
Email confirmation

### Requirements
- [ ] Create email template
- [ ] Send on booking

### Acceptance Criteria
- Email sent
- Confirmation included

### Dependencies
- Prior steps: Step 141
- Files needed: Email code

---

## Step 178: Test booking insertion after deployment
### Objective
Production verification

### Requirements
- [ ] Test on production
- [ ] Verify all features

### Acceptance Criteria
- Production works
- All tests pass

### Dependencies
- Prior steps: Step 171
- Files needed: Production URL

---

## Step 179: Create booking insertion metrics
### Objective
Track usage

### Requirements
- [ ] Count bookings
- [ ] Track success rate

### Acceptance Criteria
- Metrics logged
- Analytics available

### Dependencies
- Prior steps: Step 173
- Files needed: api/create_booking.php

---

## Step 180: Create booking insertion summary
### Objective
Compile findings

### Requirements
- [ ] Document all test results
- [ ] List issues found
- [ ] Recommendations

### Acceptance Criteria
- Complete summary
- All findings listed

### Dependencies
- Prior steps: Steps 131-179
- Files needed: Previous outputs

---

## SECTION 5: UAZAPI INTEGRATION (Steps 181-230)

---

## Step 181: Document UAZAPI base URL
### Objective
Document API URL

### Requirements
- [ ] Document URL from .env
- [ ] Document token usage

### Acceptance Criteria
- URL: https://alqueriavillacarmen.uazapi.com
- Token documented

### Dependencies
- Prior steps: Step 21
- Files needed: .env

---

## Step 182: Test UAZAPI /send/text endpoint
### Objective
Verify text sending

### Requirements
- [ ] Send test message
- [ ] Verify delivery

### Acceptance Criteria
- Message delivered
- Response correct

### Dependencies
- Prior steps: Step 6
- Files needed: send_whatsapp_uazapi.php

---

## Step 183: Test UAZAPI /send/text - Spanish characters
### Objective
Test UTF-8

### Requirements
- [ ] Send with ñ, á, é
- [ ] Verify correct display

### Acceptance Criteria
- Characters preserved
- Display correct

### Dependencies
- Prior steps: Step 182
- Files needed: send_whatsapp_uazapi.php

---

## Step 184: Test UAZAPI /send/text - Emojis
### Objective
Test emoji support

### Requirements
- [ ] Send with emojis
- [ ] Verify display

### Acceptance Criteria
- Emojis displayed
- No corruption

### Dependencies
- Prior steps: Step 182
- Files needed: send_whatsapp_uazapi.php

---

## Step 185: Test UAZAPI /send/text - Long message
### Objective
Test message limits

### Requirements
- [ ] Send long message
- [ ] Check truncation

### Acceptance Criteria
- Message handled
- Limit documented

### Dependencies
- Prior steps: Step 182
- Files needed: send_whatsapp_uazapi.php

---

## Step 186: Test UAZAPI /send/text - Line breaks
### Objective
Test formatting

### Requirements
- [ ] Send with \n
- [ ] Verify line breaks

### Acceptance Criteria
- Line breaks work
- Formatting correct

### Dependencies
- Prior steps: Step 182
- Files needed: send_whatsapp_uazapi.php

---

## Step 187: Test UAZAPI /send/text - Bold formatting
### Objective
Test *bold* markup

### Requirements
- [ ] Send with *text*
- [ ] Verify bold display

### Acceptance Criteria
- Bold displayed
- Markup works

### Dependencies
- Prior steps: Step 182
- Files needed: send_whatsapp_uazapi.php

---

## Step 188: Test UAZAPI /send/menu endpoint
### Objective
Verify menu sending

### Requirements
- [ ] Send menu message
- [ ] Verify buttons

### Acceptance Criteria
- Menu delivered
- Buttons work

### Dependencies
- Prior steps: Step 7
- Files needed: send_whatsapp_uazapi.php

---

## Step 189: Test UAZAPI /send/menu - Button type
### Objective
Test button messages

### Requirements
- [ ] Send type='button'
- [ ] Verify display

### Acceptance Criteria
- Buttons displayed
- Clickable

### Dependencies
- Prior steps: Step 188
- Files needed: send_whatsapp_uazapi.php

---

## Step 190: Test UAZAPI /send/menu - URL button
### Objective
Test URL buttons

### Requirements
- [ ] Add URL in choices
- [ ] Verify link works

### Acceptance Criteria
- URL button works
- Opens link

### Dependencies
- Prior steps: Step 189
- Files needed: send_whatsapp_uazapi.php

---

## Step 191: Test UAZAPI /send/menu - Multiple buttons
### Objective
Test multiple buttons

### Requirements
- [ ] Add 2-3 buttons
- [ ] Verify all work

### Acceptance Criteria
- All buttons displayed
- All clickable

### Dependencies
- Prior steps: Step 189
- Files needed: send_whatsapp_uazapi.php

---

## Step 192: Test UAZAPI /send/menu - List type
### Objective
Test list menus

### Requirements
- [ ] Send type='list'
- [ ] Verify display

### Acceptance Criteria
- List displayed
- Selection works

### Dependencies
- Prior steps: Step 188
- Files needed: send_whatsapp_uazapi.php

---

## Step 193: Document UAZAPI error codes
### Objective
Document errors

### Requirements
- [ ] Document HTTP codes
- [ ] Document error responses

### Acceptance Criteria
- All codes documented
- Handling clear

### Dependencies
- Prior steps: Step 182
- Files needed: None

---

## Step 194: Test UAZAPI invalid token
### Objective
Test authentication

### Requirements
- [ ] Send with wrong token
- [ ] Verify rejection

### Acceptance Criteria
- Request rejected
- Error clear

### Dependencies
- Prior steps: Step 182
- Files needed: send_whatsapp_uazapi.php

---

## Step 195: Test UAZAPI invalid phone number
### Objective
Test validation

### Requirements
- [ ] Send to invalid number
- [ ] Verify handling

### Acceptance Criteria
- Error returned
- Message clear

### Dependencies
- Prior steps: Step 182
- Files needed: send_whatsapp_uazapi.php

---

## Step 196: Test UAZAPI rate limiting
### Objective
Test limits

### Requirements
- [ ] Send many messages quickly
- [ ] Check rate limit response

### Acceptance Criteria
- Rate limit documented
- Handled gracefully

### Dependencies
- Prior steps: Step 182
- Files needed: send_whatsapp_uazapi.php

---

## Step 197: Test UAZAPI timeout handling
### Objective
Handle slow responses

### Requirements
- [ ] Set timeout
- [ ] Test handling

### Acceptance Criteria
- Timeout works
- Retry logic

### Dependencies
- Prior steps: Step 182
- Files needed: send_whatsapp_uazapi.php

---

## Step 198: Create UAZAPI wrapper class
### Objective
Centralize API calls

### Requirements
- [ ] Create UazApiClient class
- [ ] Methods for send/text, send/menu

### Acceptance Criteria
- Class created
- Methods work

### Dependencies
- Prior steps: Steps 182-197
- Files needed: New PHP file

---

## Step 199: Test UAZAPI wrapper - send text
### Objective
Test wrapper method

### Requirements
- [ ] Call sendText method
- [ ] Verify delivery

### Acceptance Criteria
- Method works
- Message sent

### Dependencies
- Prior steps: Step 198
- Files needed: UazApiClient class

---

## Step 200: Test UAZAPI wrapper - send menu
### Objective
Test wrapper method

### Requirements
- [ ] Call sendMenu method
- [ ] Verify delivery

### Acceptance Criteria
- Method works
- Menu sent

### Dependencies
- Prior steps: Step 198
- Files needed: UazApiClient class

---

## Step 201: Create UAZAPI error handling
### Objective
Handle API errors

### Requirements
- [ ] Create error handler
- [ ] Log errors
- [ ] Notify on failure

### Acceptance Criteria
- Errors handled
- Logged properly

### Dependencies
- Prior steps: Step 193
- Files needed: UazApiClient class

---

## Step 202: Test UAZAPI retry logic
### Objective
Implement retries

### Requirements
- [ ] Add retry on failure
- [ ] Test retry behavior

### Acceptance Criteria
- Retries work
- Max attempts respected

### Dependencies
- Prior steps: Step 201
- Files needed: UazApiClient class

---

## Step 203: Test UAZAPI message queuing
### Objective
Queue messages

### Requirements
- [ ] Queue multiple messages
- [ ] Process sequentially

### Acceptance Criteria
- Queue works
- All sent

### Dependencies
- Prior steps: Step 198
- Files needed: UazApiClient class

---

## Step 204: Create UAZAPI logging
### Objective
Log all API calls

### Requirements
- [ ] Log requests
- [ ] Log responses

### Acceptance Criteria
- All logged
- Debug available

### Dependencies
- Prior steps: Step 198
- Files needed: UazApiClient class

---

## Step 205: Test UAZAPI performance
### Objective
Measure latency

### Requirements
- [ ] Time API calls
- [ ] Document average

### Acceptance Criteria
- Latency documented
- Acceptable performance

### Dependencies
- Prior steps: Step 198
- Files needed: UazApiClient class

---

## Step 206: Document UAZAPI integration
### Objective
Create documentation

### Requirements
- [ ] Document all methods
- [ ] Include examples

### Acceptance Criteria
- Complete docs
- Clear examples

### Dependencies
- Prior steps: Steps 181-205
- Files needed: None

---

## Step 207: Test UAZAPI in C# WhatsAppService
### Objective
Verify C# integration

### Requirements
- [ ] Test SendTextAsync
- [ ] Verify delivery

### Acceptance Criteria
- C# integration works
- Messages sent

### Dependencies
- Prior steps: Step 10
- Files needed: WhatsAppService.cs

---

## Step 208: Test UAZAPI C# SendMenuAsync
### Objective
Test menu from C#

### Requirements
- [ ] Call SendMenuAsync
- [ ] Verify delivery

### Acceptance Criteria
- Method works
- Menu sent

### Dependencies
- Prior steps: Step 207
- Files needed: WhatsAppService.cs

---

## Step 209: Create UAZAPI health check
### Objective
Monitor API status

### Requirements
- [ ] Create health endpoint
- [ ] Check API availability

### Acceptance Criteria
- Health check works
- Status reported

### Dependencies
- Prior steps: Step 198
- Files needed: New PHP file

---

## Step 210: Test UAZAPI health check
### Objective
Verify health check

### Requirements
- [ ] Call health endpoint
- [ ] Verify response

### Acceptance Criteria
- Status returned
- Accurate report

### Dependencies
- Prior steps: Step 209
- Files needed: Health check endpoint

---

## Step 211: Create UAZAPI fallback mechanism
### Objective
Handle API outage

### Requirements
- [ ] Detect outage
- [ ] Fallback behavior

### Acceptance Criteria
- Fallback works
- User informed

### Dependencies
- Prior steps: Step 201
- Files needed: UazApiClient class

---

## Step 212: Test UAZAPI with conversation flow
### Objective
Integration test

### Requirements
- [ ] Complete conversation
- [ ] Verify all messages

### Acceptance Criteria
- Flow works
- All messages sent

### Dependencies
- Prior steps: Steps 181-211
- Files needed: All UAZAPI code

---

## Step 213: Document UAZAPI message templates
### Objective
Document templates

### Requirements
- [ ] List all templates
- [ ] Document variables

### Acceptance Criteria
- Templates documented
- Variables clear

### Dependencies
- Prior steps: Step 212
- Files needed: None

---

## Step 214: Create UAZAPI message templates
### Objective
Create reusable templates

### Requirements
- [ ] Create booking confirmation template
- [ ] Create notification template

### Acceptance Criteria
- Templates created
- Reusable

### Dependencies
- Prior steps: Step 213
- Files needed: Template code

---

## Step 215: Test UAZAPI booking confirmation template
### Objective
Test template

### Requirements
- [ ] Use template
- [ ] Verify message

### Acceptance Criteria
- Template works
- Variables replaced

### Dependencies
- Prior steps: Step 214
- Files needed: Template code

---

## Step 216: Test UAZAPI notification template
### Objective
Test template

### Requirements
- [ ] Use template
- [ ] Verify message

### Acceptance Criteria
- Template works
- Variables replaced

### Dependencies
- Prior steps: Step 214
- Files needed: Template code

---

## Step 217: Create UAZAPI metrics
### Objective
Track API usage

### Requirements
- [ ] Count calls
- [ ] Track success rate

### Acceptance Criteria
- Metrics logged
- Analytics available

### Dependencies
- Prior steps: Step 204
- Files needed: UazApiClient class

---

## Step 218: Test UAZAPI after deployment
### Objective
Production verification

### Requirements
- [ ] Test on production
- [ ] Verify all features

### Acceptance Criteria
- Production works
- All tests pass

### Dependencies
- Prior steps: Step 212
- Files needed: Production URL

---

## Step 219: Create UAZAPI security review
### Objective
Review security

### Requirements
- [ ] Check token handling
- [ ] Verify HTTPS

### Acceptance Criteria
- Security verified
- No issues

### Dependencies
- Prior steps: Step 198
- Files needed: All UAZAPI code

---

## Step 220: Document UAZAPI best practices
### Objective
Document guidelines

### Requirements
- [ ] Document usage patterns
- [ ] List best practices

### Acceptance Criteria
- Guidelines documented
- Clear recommendations

### Dependencies
- Prior steps: Step 206
- Files needed: None

---

## Step 221: Test UAZAPI message delivery confirmation
### Objective
Track delivery

### Requirements
- [ ] Check delivery status
- [ ] Handle failed delivery

### Acceptance Criteria
- Status trackable
- Failures handled

### Dependencies
- Prior steps: Step 182
- Files needed: UazApiClient class

---

## Step 222: Create UAZAPI webhook handler
### Objective
Handle callbacks

### Requirements
- [ ] Create webhook endpoint
- [ ] Process delivery updates

### Acceptance Criteria
- Webhook works
- Updates processed

### Dependencies
- Prior steps: Step 221
- Files needed: New PHP file

---

## Step 223: Test UAZAPI webhook
### Objective
Verify webhook

### Requirements
- [ ] Receive callback
- [ ] Process correctly

### Acceptance Criteria
- Callback received
- Processed correctly

### Dependencies
- Prior steps: Step 222
- Files needed: Webhook endpoint

---

## Step 224: Create UAZAPI conversation threading
### Objective
Track conversations

### Requirements
- [ ] Track message IDs
- [ ] Link replies

### Acceptance Criteria
- Threading works
- Context maintained

### Dependencies
- Prior steps: Step 222
- Files needed: Conversation tracking

---

## Step 225: Test UAZAPI threading
### Objective
Verify threading

### Requirements
- [ ] Send threaded messages
- [ ] Verify context

### Acceptance Criteria
- Threading works
- Context correct

### Dependencies
- Prior steps: Step 224
- Files needed: Threading code

---

## Step 226: Create UAZAPI summary report
### Objective
Compile findings

### Requirements
- [ ] Document all tests
- [ ] List issues
- [ ] Recommendations

### Acceptance Criteria
- Complete summary
- All findings listed

### Dependencies
- Prior steps: Steps 181-225
- Files needed: Previous outputs

---

## Step 227: Test UAZAPI with real phone numbers
### Objective
Real-world test

### Requirements
- [ ] Test with actual numbers
- [ ] Verify delivery

### Acceptance Criteria
- Messages delivered
- Real-world works

### Dependencies
- Prior steps: Step 218
- Files needed: Production API

---

## Step 228: Create UAZAPI notification preferences
### Objective
Manage notifications

### Requirements
- [ ] Allow opt-out
- [ ] Respect preferences

### Acceptance Criteria
- Preferences work
- Opt-out respected

### Dependencies
- Prior steps: Step 222
- Files needed: Preferences code

---

## Step 229: Test UAZAPI notification preferences
### Objective
Verify preferences

### Requirements
- [ ] Set preference
- [ ] Verify respected

### Acceptance Criteria
- Preference works
- No unwanted messages

### Dependencies
- Prior steps: Step 228
- Files needed: Preferences code

---

## Step 230: Final UAZAPI verification
### Objective
Complete verification

### Requirements
- [ ] Run all tests
- [ ] Verify all scenarios
- [ ] Document results

### Acceptance Criteria
- All tests pass
- Documentation complete

### Dependencies
- Prior steps: Steps 181-229
- Files needed: All UAZAPI files

---

## SECTION 6: PHONE NUMBER FORMATTING (Steps 231-250)

---

## Step 231: Create phone formatting utility
### Objective
Create utility function

### Requirements
- [ ] Create formatPhone function
- [ ] Handle all formats

### Acceptance Criteria
- Function created
- All formats handled

### Dependencies
- Prior steps: Step 146
- Files needed: New utility file

---

## Step 232: Test phone formatting - International format
### Objective
Test +34 format

### Requirements
- [ ] Input: +34612345678
- [ ] Output: 612345678

### Acceptance Criteria
- Correct output
- +34 removed

### Dependencies
- Prior steps: Step 231
- Files needed: Phone utility

---

## Step 233: Test phone formatting - Country code without plus
### Objective
Test 34 format

### Requirements
- [ ] Input: 34612345678
- [ ] Output: 612345678

### Acceptance Criteria
- Correct output
- 34 removed

### Dependencies
- Prior steps: Step 231
- Files needed: Phone utility

---

## Step 234: Test phone formatting - Local format
### Objective
Test 9-digit format

### Requirements
- [ ] Input: 612345678
- [ ] Output: 612345678

### Acceptance Criteria
- No change needed
- Correct output

### Dependencies
- Prior steps: Step 231
- Files needed: Phone utility

---

## Step 235: Test phone formatting - With spaces
### Objective
Test space handling

### Requirements
- [ ] Input: 612 345 678
- [ ] Output: 612345678

### Acceptance Criteria
- Spaces removed
- Correct output

### Dependencies
- Prior steps: Step 231
- Files needed: Phone utility

---

## Step 236: Test phone formatting - With dashes
### Objective
Test dash handling

### Requirements
- [ ] Input: 612-345-678
- [ ] Output: 612345678

### Acceptance Criteria
- Dashes removed
- Correct output

### Dependencies
- Prior steps: Step 231
- Files needed: Phone utility

---

## Step 237: Test phone formatting - With parentheses
### Objective
Test parentheses handling

### Requirements
- [ ] Input: (612) 345678
- [ ] Output: 612345678

### Acceptance Criteria
- Parentheses removed
- Correct output

### Dependencies
- Prior steps: Step 231
- Files needed: Phone utility

---

## Step 238: Test phone formatting - Mixed format
### Objective
Test combined handling

### Requirements
- [ ] Input: +34 (612) 345-678
- [ ] Output: 612345678

### Acceptance Criteria
- All cleaned
- Correct output

### Dependencies
- Prior steps: Step 231
- Files needed: Phone utility

---

## Step 239: Test phone validation - Too short
### Objective
Reject invalid

### Requirements
- [ ] Input: 12345
- [ ] Reject as invalid

### Acceptance Criteria
- Validation fails
- Error returned

### Dependencies
- Prior steps: Step 231
- Files needed: Phone utility

---

## Step 240: Test phone validation - Too long
### Objective
Reject invalid

### Requirements
- [ ] Input: 1234567890123
- [ ] Reject as invalid

### Acceptance Criteria
- Validation fails
- Error returned

### Dependencies
- Prior steps: Step 231
- Files needed: Phone utility

---

## Step 241: Test phone validation - Non-numeric
### Objective
Reject invalid

### Requirements
- [ ] Input: 612ABC789
- [ ] Handle appropriately

### Acceptance Criteria
- Letters removed or rejected
- Handled appropriately

### Dependencies
- Prior steps: Step 231
- Files needed: Phone utility

---

## Step 242: Integrate phone formatting with booking
### Objective
Use in booking flow

### Requirements
- [ ] Call formatter before insert
- [ ] Verify correct storage

### Acceptance Criteria
- Integration works
- Phone formatted

### Dependencies
- Prior steps: Steps 231-241
- Files needed: insert_booking.php

---

## Step 243: Test phone formatting in booking flow
### Objective
End-to-end test

### Requirements
- [ ] Submit booking with +34
- [ ] Verify stored format

### Acceptance Criteria
- Phone formatted
- Stored correctly

### Dependencies
- Prior steps: Step 242
- Files needed: insert_booking.php

---

## Step 244: Integrate phone formatting with notifications
### Objective
Format for UAZAPI

### Requirements
- [ ] Format before API call
- [ ] Verify delivery

### Acceptance Criteria
- Formatted correctly
- Message delivered

### Dependencies
- Prior steps: Step 242
- Files needed: send_whatsapp_uazapi.php

---

## Step 245: Document phone formatting rules
### Objective
Create documentation

### Requirements
- [ ] Document all rules
- [ ] Include examples

### Acceptance Criteria
- Rules documented
- Examples clear

### Dependencies
- Prior steps: Steps 231-244
- Files needed: None

---

## Step 246: Test phone formatting performance
### Objective
Measure performance

### Requirements
- [ ] Time function calls
- [ ] Document speed

### Acceptance Criteria
- Fast execution
- Performance acceptable

### Dependencies
- Prior steps: Step 231
- Files needed: Phone utility

---

## Step 247: Create phone formatting unit tests
### Objective
Create test suite

### Requirements
- [ ] Test all formats
- [ ] Test edge cases

### Acceptance Criteria
- All tests pass
- Coverage complete

### Dependencies
- Prior steps: Steps 231-241
- Files needed: Test file

---

## Step 248: Test phone formatting in C#
### Objective
C# implementation

### Requirements
- [ ] Implement in C#
- [ ] Verify consistency

### Acceptance Criteria
- Same behavior
- Consistent output

### Dependencies
- Prior steps: Step 231
- Files needed: C# code

---

## Step 249: Test phone formatting after deployment
### Objective
Production verification

### Requirements
- [ ] Test on production
- [ ] Verify all formats

### Acceptance Criteria
- Production works
- All tests pass

### Dependencies
- Prior steps: Step 247
- Files needed: Production URL

---

## Step 250: Create phone formatting summary
### Objective
Compile findings

### Requirements
- [ ] Document all tests
- [ ] List issues
- [ ] Recommendations

### Acceptance Criteria
- Complete summary
- All findings listed

### Dependencies
- Prior steps: Steps 231-249
- Files needed: Previous outputs

---

## SECTION 7: NOTIFICATION SYSTEM (Steps 251-280)

---

## Step 251: Create notification service class
### Objective
Centralize notifications

### Requirements
- [ ] Create NotificationService class
- [ ] Methods for all notifications

### Acceptance Criteria
- Class created
- Methods work

### Dependencies
- Prior steps: Step 153
- Files needed: New PHP file

---

## Step 252: Test notification service - Customer message
### Objective
Test customer notification

### Requirements
- [ ] Send customer message
- [ ] Verify delivery

### Acceptance Criteria
- Message sent
- Delivered correctly

### Dependencies
- Prior steps: Step 251
- Files needed: NotificationService

---

## Step 253: Test notification service - Restaurant notification
### Objective
Test owner notifications

### Requirements
- [ ] Send to all 3 owners
- [ ] Verify all delivered

### Acceptance Criteria
- All 3 notified
- Content correct

### Dependencies
- Prior steps: Step 251
- Files needed: NotificationService

---

## Step 254: Create notification message formatting
### Objective
Format messages

### Requirements
- [ ] Create message templates
- [ ] Handle variable substitution

### Acceptance Criteria
- Templates work
- Variables replaced

### Dependencies
- Prior steps: Step 251
- Files needed: NotificationService

---

## Step 255: Test notification format - Booking details
### Objective
Verify format

### Requirements
- [ ] Include all booking fields
- [ ] Verify readability

### Acceptance Criteria
- All fields included
- Readable format

### Dependencies
- Prior steps: Step 254
- Files needed: NotificationService

---

## Step 256: Test notification format - AI booking identifier
### Objective
Include AI prefix

### Requirements
- [ ] Add "Reserva hecha por asistente de IA:"
- [ ] Verify included

### Acceptance Criteria
- Prefix included
- Clearly marked

### Dependencies
- Prior steps: Step 254
- Files needed: NotificationService

---

## Step 257: Create notification queue
### Objective
Queue notifications

### Requirements
- [ ] Queue messages
- [ ] Process in order

### Acceptance Criteria
- Queue works
- All processed

### Dependencies
- Prior steps: Step 251
- Files needed: NotificationService

---

## Step 258: Test notification queue
### Objective
Verify queue

### Requirements
- [ ] Add multiple notifications
- [ ] Verify processing

### Acceptance Criteria
- All processed
- Order correct

### Dependencies
- Prior steps: Step 257
- Files needed: NotificationService

---

## Step 259: Create notification error handling
### Objective
Handle failures

### Requirements
- [ ] Catch failures
- [ ] Log errors
- [ ] Retry logic

### Acceptance Criteria
- Failures handled
- Retries work

### Dependencies
- Prior steps: Step 251
- Files needed: NotificationService

---

## Step 260: Test notification retry logic
### Objective
Verify retries

### Requirements
- [ ] Simulate failure
- [ ] Verify retry

### Acceptance Criteria
- Retry occurs
- Max attempts respected

### Dependencies
- Prior steps: Step 259
- Files needed: NotificationService

---

## Step 261: Create notification logging
### Objective
Log notifications

### Requirements
- [ ] Log all sends
- [ ] Log failures

### Acceptance Criteria
- All logged
- Debug available

### Dependencies
- Prior steps: Step 251
- Files needed: NotificationService

---

## Step 262: Test notification logging
### Objective
Verify logging

### Requirements
- [ ] Send notification
- [ ] Check logs

### Acceptance Criteria
- Entry logged
- Details correct

### Dependencies
- Prior steps: Step 261
- Files needed: NotificationService

---

## Step 263: Create notification preferences
### Objective
Manage preferences

### Requirements
- [ ] Store preferences
- [ ] Respect opt-out

### Acceptance Criteria
- Preferences work
- Opt-out respected

### Dependencies
- Prior steps: Step 251
- Files needed: Preferences table

---

## Step 264: Test notification preferences
### Objective
Verify preferences

### Requirements
- [ ] Set opt-out
- [ ] Verify no message

### Acceptance Criteria
- Preference respected
- No unwanted messages

### Dependencies
- Prior steps: Step 263
- Files needed: Preferences system

---

## Step 265: Create notification analytics
### Objective
Track metrics

### Requirements
- [ ] Count notifications
- [ ] Track success rate

### Acceptance Criteria
- Metrics tracked
- Analytics available

### Dependencies
- Prior steps: Step 261
- Files needed: NotificationService

---

## Step 266: Test notification analytics
### Objective
Verify analytics

### Requirements
- [ ] Send notifications
- [ ] Check metrics

### Acceptance Criteria
- Counts correct
- Rates accurate

### Dependencies
- Prior steps: Step 265
- Files needed: Analytics system

---

## Step 267: Document notification system
### Objective
Create documentation

### Requirements
- [ ] Document all methods
- [ ] Include examples

### Acceptance Criteria
- Complete docs
- Clear examples

### Dependencies
- Prior steps: Steps 251-266
- Files needed: None

---

## Step 268: Test notification performance
### Objective
Measure speed

### Requirements
- [ ] Time notifications
- [ ] Document latency

### Acceptance Criteria
- Acceptable speed
- Performance documented

### Dependencies
- Prior steps: Step 251
- Files needed: NotificationService

---

## Step 269: Test notification concurrent access
### Objective
Test concurrency

### Requirements
- [ ] Multiple simultaneous sends
- [ ] Verify all delivered

### Acceptance Criteria
- No race conditions
- All delivered

### Dependencies
- Prior steps: Step 268
- Files needed: NotificationService

---

## Step 270: Create notification templates
### Objective
Create reusable templates

### Requirements
- [ ] Create booking template
- [ ] Create cancellation template

### Acceptance Criteria
- Templates created
- Reusable

### Dependencies
- Prior steps: Step 254
- Files needed: Template files

---

## Step 271: Test booking notification template
### Objective
Verify template

### Requirements
- [ ] Use template
- [ ] Verify content

### Acceptance Criteria
- Template works
- Content correct

### Dependencies
- Prior steps: Step 270
- Files needed: Template files

---

## Step 272: Test notification to first owner
### Objective
Verify delivery

### Requirements
- [ ] Send to 34686969914
- [ ] Verify received

### Acceptance Criteria
- Message delivered
- Content correct

### Dependencies
- Prior steps: Step 253
- Files needed: NotificationService

---

## Step 273: Test notification to second owner
### Objective
Verify delivery

### Requirements
- [ ] Send to 34638857294
- [ ] Verify received

### Acceptance Criteria
- Message delivered
- Content correct

### Dependencies
- Prior steps: Step 253
- Files needed: NotificationService

---

## Step 274: Test notification to third owner
### Objective
Verify delivery

### Requirements
- [ ] Send to 34692747052
- [ ] Verify received

### Acceptance Criteria
- Message delivered
- Content correct

### Dependencies
- Prior steps: Step 253
- Files needed: NotificationService

---

## Step 275: Create notification failure alerting
### Objective
Alert on failures

### Requirements
- [ ] Detect failures
- [ ] Alert system admin

### Acceptance Criteria
- Failures detected
- Alerts sent

### Dependencies
- Prior steps: Step 259
- Files needed: Alerting system

---

## Step 276: Test notification after deployment
### Objective
Production verification

### Requirements
- [ ] Test on production
- [ ] Verify all notifications

### Acceptance Criteria
- Production works
- All tests pass

### Dependencies
- Prior steps: Step 268
- Files needed: Production URL

---

## Step 277: Create notification security review
### Objective
Review security

### Requirements
- [ ] Check data handling
- [ ] Verify privacy

### Acceptance Criteria
- Security verified
- No issues

### Dependencies
- Prior steps: Step 251
- Files needed: All notification code

---

## Step 278: Document notification best practices
### Objective
Document guidelines

### Requirements
- [ ] Document patterns
- [ ] List recommendations

### Acceptance Criteria
- Guidelines documented
- Clear recommendations

### Dependencies
- Prior steps: Step 267
- Files needed: None

---

## Step 279: Create notification summary report
### Objective
Compile findings

### Requirements
- [ ] Document all tests
- [ ] List issues
- [ ] Recommendations

### Acceptance Criteria
- Complete summary
- All findings listed

### Dependencies
- Prior steps: Steps 251-278
- Files needed: Previous outputs

---

## Step 280: Final notification verification
### Objective
Complete verification

### Requirements
- [ ] Run all tests
- [ ] Verify all scenarios

### Acceptance Criteria
- All tests pass
- Documentation complete

### Dependencies
- Prior steps: Steps 251-279
- Files needed: All notification files

---

## SECTION 8: END-TO-END WORKFLOW (Steps 281-295)

---

## Step 281: Create complete booking scenario
### Objective
Full workflow test

### Requirements
- [ ] Define test scenario
- [ ] All fields included

### Acceptance Criteria
- Scenario defined
- All steps covered

### Dependencies
- Prior steps: Sections 1-7
- Files needed: All workflow files

---

## Step 282: Test workflow - User provides date
### Objective
Test date step

### Requirements
- [ ] User sends date
- [ ] Validate via API

### Acceptance Criteria
- Date validated
- Conversation continues

### Dependencies
- Prior steps: Step 281
- Files needed: Date validation

---

## Step 283: Test workflow - Invalid date handling
### Objective
Test error path

### Requirements
- [ ] User sends closed day
- [ ] UAZAPI message sent

### Acceptance Criteria
- Alternative suggested
- WhatsApp sent

### Dependencies
- Prior steps: Step 282
- Files needed: Date validation

---

## Step 284: Test workflow - User provides time
### Objective
Test time step

### Requirements
- [ ] User sends time
- [ ] Validate via API

### Acceptance Criteria
- Time validated
- Conversation continues

### Dependencies
- Prior steps: Step 282
- Files needed: Date validation

---

## Step 285: Test workflow - Invalid time handling
### Objective
Test error path

### Requirements
- [ ] User sends invalid time
- [ ] Alternative offered

### Acceptance Criteria
- Error handled
- Alternatives shown

### Dependencies
- Prior steps: Step 284
- Files needed: Date validation

---

## Step 286: Test workflow - User provides party size
### Objective
Test party size step

### Requirements
- [ ] User sends number
- [ ] Validate

### Acceptance Criteria
- Size validated
- Conversation continues

### Dependencies
- Prior steps: Step 284
- Files needed: Workflow code

---

## Step 287: Test workflow - User provides rice type
### Objective
Test rice step

### Requirements
- [ ] User sends rice type
- [ ] Validate via API

### Acceptance Criteria
- Rice validated
- Conversation continues

### Dependencies
- Prior steps: Step 286
- Files needed: Rice validation

---

## Step 288: Test workflow - Invalid rice handling
### Objective
Test error path

### Requirements
- [ ] User sends invalid rice
- [ ] Menu sent via UAZAPI

### Acceptance Criteria
- Menu button sent
- URL included

### Dependencies
- Prior steps: Step 287
- Files needed: Rice validation

---

## Step 289: Test workflow - User declines rice
### Objective
Test no-rice path

### Requirements
- [ ] User says "sin arroz"
- [ ] Set null values

### Acceptance Criteria
- Rice fields null
- No further questions

### Dependencies
- Prior steps: Step 287
- Files needed: Rice validation

---

## Step 290: Test workflow - User provides baby equipment
### Objective
Test equipment step

### Requirements
- [ ] User sends tronas/carritos
- [ ] Extract values

### Acceptance Criteria
- Values extracted
- Conversation continues

### Dependencies
- Prior steps: Step 287
- Files needed: Workflow code

---

## Step 291: Test workflow - Complete booking
### Objective
Test final booking

### Requirements
- [ ] All data collected
- [ ] Booking created

### Acceptance Criteria
- Booking inserted
- ID returned

### Dependencies
- Prior steps: Step 290
- Files needed: insert_booking.php

---

## Step 292: Test workflow - Customer notification
### Objective
Verify customer message

### Requirements
- [ ] Booking created
- [ ] Customer notified

### Acceptance Criteria
- Message sent
- Confirmation received

### Dependencies
- Prior steps: Step 291
- Files needed: NotificationService

---

## Step 293: Test workflow - Restaurant notifications
### Objective
Verify owner messages

### Requirements
- [ ] Booking created
- [ ] All 3 owners notified

### Acceptance Criteria
- All 3 receive message
- "Reserva hecha por IA" prefix

### Dependencies
- Prior steps: Step 291
- Files needed: NotificationService

---

## Step 294: Test workflow - Full conversation
### Objective
Complete conversation test

### Requirements
- [ ] Multi-turn conversation
- [ ] All steps completed

### Acceptance Criteria
- Conversation flows naturally
- Booking successful

### Dependencies
- Prior steps: Steps 281-293
- Files needed: All workflow files

---

## Step 295: Create workflow test suite
### Objective
Automated tests

### Requirements
- [ ] Create test suite
- [ ] Cover all scenarios

### Acceptance Criteria
- Tests automated
- All scenarios covered

### Dependencies
- Prior steps: Steps 281-294
- Files needed: Test framework

---

## SECTION 9: DEPLOYMENT & VERIFICATION (Steps 296-300)

---

## Step 296: Prepare deployment package
### Objective
Package for deployment

### Requirements
- [ ] List all new files
- [ ] Verify dependencies

### Acceptance Criteria
- Package ready
- Dependencies met

### Dependencies
- Prior steps: Steps 1-295
- Files needed: All project files

---

## Step 297: Run deploy.sh
### Objective
Deploy to production

### Requirements
- [ ] Execute deploy.sh
- [ ] Verify success

### Acceptance Criteria
- Deployment successful
- Server updated

### Dependencies
- Prior steps: Step 296
- Files needed: deploy.sh

---

## Step 298: Verify production deployment
### Objective
Confirm deployment

### Requirements
- [ ] Check all files on server
- [ ] Verify API access

### Acceptance Criteria
- All files present
- APIs accessible

### Dependencies
- Prior steps: Step 297
- Files needed: Production server

---

## Step 299: Run production tests
### Objective
Production verification

### Requirements
- [ ] Run all test cases
- [ ] Verify all pass

### Acceptance Criteria
- All tests pass
- Production ready

### Dependencies
- Prior steps: Step 298
- Files needed: Test suite

---

## Step 300: Create final verification report
### Objective
Complete documentation

### Requirements
- [ ] Document all results
- [ ] List any issues
- [ ] Final recommendations

### Acceptance Criteria
- Complete report
- All verified
- Ready for use

### Dependencies
- Prior steps: Steps 1-299
- Files needed: All outputs

---

# COMPLETION CHECKLIST

- [ ] All 300 steps completed
- [ ] Date validation working
- [ ] Rice validation working
- [ ] Booking insertion working
- [ ] UAZAPI integration working
- [ ] Phone formatting working
- [ ] Notifications working
- [ ] End-to-end workflow verified
- [ ] Deployed to production
- [ ] Final report created
