#!/bin/bash
cd "$(dirname "$0")"
export PATH="/opt/homebrew/bin:$PATH"
node node_modules/.bin/vite --host 127.0.0.1 --port 9990 --mode development
