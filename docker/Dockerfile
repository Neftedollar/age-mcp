ARG PG_MAJOR=17
ARG AGE_VERSION=1.6.0

FROM apache/age:release_PG${PG_MAJOR}_${AGE_VERSION}

ARG PG_MAJOR
RUN apt-get update && \
    apt-get install -y --no-install-recommends postgresql-${PG_MAJOR}-pgvector && \
    rm -rf /var/lib/apt/lists/*
