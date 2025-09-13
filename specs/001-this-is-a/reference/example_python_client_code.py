# Example Python Code
# Snowpipe Streaming High Performance SDK

from datetime import datetime
import time
import uuid
import os

# Change Environment Variable SS_LOG_LEVEL="info" to increase logging details
os.environ["SS_LOG_LEVEL"] = "warn"

from snowflake.ingest.streaming import StreamingIngestClient


MAX_ROWS = 100_000
POLL_ATTEMPTS = 30
POLL_INTERVAL_MS = 1000


# Create Snowflake Streaming Ingest Client
client = StreamingIngestClient(
    client_name=f"MY_CLIENT_{uuid.uuid4()}",
    db_name="MY_DATABASE",
    schema_name="MY_SCHEMA",
    pipe_name="MY_PIPE",
    profile_json="profile.json",  # depends on your folder structure
)

# Open a channel for data ingestion
channel, status = client.open_channel(f"MY_CHANNEL_{uuid.uuid4()}")
print(f"Channel opened: {channel.channel_name}")

# Ingest rows
print(f"Ingesting {MAX_ROWS} rows")
for i in range(MAX_ROWS):
    row_id = str(i)
    channel.append_row({"c1": i, "c2": row_id, "ts": datetime.now()}, row_id)

# Wait for ingestion to complete
for attempt in range(POLL_ATTEMPTS):
    latest_offset = channel.get_latest_committed_offset_token()
    print(f"Latest offset token: {latest_offset}")
    if latest_offset == str(MAX_ROWS - 1):
        print("All data committed successfully")
        break
    time.sleep(POLL_INTERVAL_MS / 1000)
else:
    raise Exception("Ingestion failed after all attempts")

# Close resources
channel.close()
client.close()

print("Data ingestion completed")
