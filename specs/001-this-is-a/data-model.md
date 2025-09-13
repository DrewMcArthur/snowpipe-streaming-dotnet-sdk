# Data Model: Snowpipe Streaming .NET SDK

Note: Field names and types must mirror the Snowflake REST spec exactly. Placeholders below are to be completed from the official documentation.

## Exchange Scoped Token (form fields)
- grant_type: string = "urn:ietf:params:oauth:grant-type:jwt-bearer"
- scope: string (account hostname)

## ScopedTokenResponse
- token: string

## OpenChannelRequest (JSON)
- offset_token: string (optional)

## OpenChannelResponse (JSON)
- next_continuation_token: string
- channel_status: object
  - database_name: string
  - schema_name: string
  - pipe_name: string
  - channel_name: string
  - channel_status_code: string
  - last_committed_offset_token: string
  - created_on_ms: long
  - rows_inserted: int
  - rows_parsed: int
  - rows_error_count: int
  - last_error_offset_upper_bound: string
  - last_error_message: string
  - last_error_timestamp: long
  - snowflake_avg_processing_latency_ms: int

## AppendRowsRequest
- Query params:
  - continuationToken: string (required)
  - offsetToken: string (optional)
  - requestId: uuid (optional)
- Body: NDJSON (application/x-ndjson). Each line is a JSON text per RFC 8259 terminated by \n (0x0A). Optional \r before \n.

## AppendRowsResponse
- next_continuation_token: string

## AppendError
- index: integer (row index)
- code: string
- message: string
- details: object

## BulkChannelStatusRequest
- channel_names: array<string>

## BulkChannelStatusResponse
- channel_statuses: map<string, ChannelStatus>

## ChannelStatus
- channel_status_code: string
- last_committed_offset_token: string
- database_name: string
- schema_name: string
- pipe_name: string
- channel_name: string
- rows_inserted: int
- rows_parsed: int
- rows_errors: int
- last_error_offset_upper_bound: string
- last_error_message: string
- last_error_timestamp: timestamp_utc (string or epoch ms; see spec copy)
- snowflake_avg_processing_latency_ms: int

## ErrorResponse
- error_code: string
- message: string
