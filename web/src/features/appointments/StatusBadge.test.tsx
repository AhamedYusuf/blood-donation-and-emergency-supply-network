import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatusBadge, STATUS_CONFIG } from "./StatusBadge";

describe("StatusBadge", () => {
  it.each(Object.keys(STATUS_CONFIG))("renders the correct label for status %s", (status) => {
    render(<StatusBadge status={status} />);
    expect(screen.getByText(STATUS_CONFIG[status].label)).toBeInTheDocument();
  });

  it("falls back to the cancelled style for an unrecognized status", () => {
    render(<StatusBadge status="not_a_real_status" />);
    expect(screen.getByText(STATUS_CONFIG.cancelled.label)).toBeInTheDocument();
  });
});
