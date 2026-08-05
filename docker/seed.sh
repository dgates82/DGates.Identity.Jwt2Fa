#!/bin/sh
# Runs automatically inside the LocalStack container via /etc/localstack/init/ready.d.
# Do not run this manually against a real AWS account.

set -e

BUCKET_NAME="${NOTES_BUCKET_NAME:-notes-bucket}"
REGION="${AWS_DEFAULT_REGION:-us-west-2}"

echo "Seeding LocalStack: creating bucket '${BUCKET_NAME}' in ${REGION}..."

awslocal s3api create-bucket \
  --bucket "${BUCKET_NAME}" \
  --region "${REGION}" \
  --create-bucket-configuration LocationConstraint="${REGION}"

echo "Seeding starting note..."

NOTE_ID="seed-note-1"
NOTE_TITLE="Welcome to ExampleLibrary"
NOTE_CREATED_AT="2026-01-01T00:00:00Z"
NOTE_FILE="/tmp/${NOTE_ID}.json"

cat > "${NOTE_FILE}" <<EOF
{"id":"${NOTE_ID}","title":"${NOTE_TITLE}","body":"This note was seeded automatically for local dev and integration tests.","createdAt":"${NOTE_CREATED_AT}"}
EOF

awslocal s3api put-object \
  --bucket "${BUCKET_NAME}" \
  --key "notes/${NOTE_ID}.json" \
  --body "${NOTE_FILE}" \
  --metadata title="${NOTE_TITLE}",created-at="${NOTE_CREATED_AT}"

rm -f "${NOTE_FILE}"

echo "Seed complete: bucket '${BUCKET_NAME}' ready with note '${NOTE_ID}'."