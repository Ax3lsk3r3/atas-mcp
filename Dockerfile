# syntax=docker/dockerfile:1
FROM python:3.11-slim

WORKDIR /app

# Prevent Python from writing pyc files and buffer stdout/stderr
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    MCP_TRANSPORT=sse \
    MCP_HOST=0.0.0.0 \
    MCP_PORT=8000

# Install dependencies
COPY requirements.txt pyproject.toml ./
RUN pip install --no-cache-dir -r requirements.txt

# Copy source files
COPY server.py atas_client.py ./
RUN pip install --no-cache-dir -e .

EXPOSE 8000

# Run in SSE mode by default for containerized environments
ENTRYPOINT ["python", "server.py"]
CMD ["--transport", "sse", "--host", "0.0.0.0", "--port", "8000"]
