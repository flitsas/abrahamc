import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { EmptyState } from "./EmptyState.js";

describe("EmptyState", () => {
  it("renders title and description", () => {
    render(
      <EmptyState
        title="Sin datos"
        description="No hay registros disponibles."
      />,
    );

    expect(
      screen.getByRole("heading", { name: "Sin datos" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("No hay registros disponibles."),
    ).toBeInTheDocument();
  });
});
